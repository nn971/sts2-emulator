using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryTrialTests
{
    private static readonly PrototypeGameEngine Engine = new();
    private const string Trial = PrototypeNativeGloryEvents.TrialId;

    [Fact]
    public void TrialAndCursesAreRegisteredButRemainOutsideRandomPools()
    {
        var evt = PrototypeContent.Event(Trial);
        Assert.Equal((3, 3, 0),
            (evt.MinAct, evt.MaxAct, evt.Weight));
        foreach (var id in new[]
        {
            PrototypeNativeGloryEvents.RegretId,
            PrototypeNativeGloryEvents.ShameId,
            PrototypeNativeGloryEvents.DoubtId
        })
        {
            var curse = PrototypeContent.Card(id);
            Assert.Equal(PrototypeCardType.Curse, curse.Type);
            Assert.Equal(0, curse.MaxUpgradeLevel);
            Assert.True(curse.Unplayable);
            Assert.DoesNotContain(id, PrototypeContent.RewardCardPool);
        }
        Assert.Equal(new[] { "accept", "reject" }, Choices(Glory("initial")));
    }

    [Fact]
    public void AcceptSelectsAllThreeCasesAndForksStayDeterministic()
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < 72; i++)
        {
            var state = Glory($"trial-branch-{i}");
            var initialHash = CanonicalJson.Sha256(state);
            var caseState = Choose(state, "accept");
            Assert.Equal(RunPhase.Event, caseState.Phase);
            Assert.InRange(caseState.World!.Event!.NativePageIndex, 2, 4);
            seen.Add(caseState.World.Event.NativePageIndex);
            Assert.Equal(initialHash, CanonicalJson.Sha256(state));
            Assert.Equal(CanonicalJson.Sha256(caseState),
                CanonicalJson.Sha256(Choose(state.Fork(), "accept")));
            Assert.Equal(2, Choices(caseState).Length);
            PrototypeStateInvariants.Validate(caseState);
        }
        Assert.True(seen.SetEquals(new[] { 2, 3, 4 }));
    }

    [Fact]
    public void RejectReconsiderAndAbandonRequireExplicitConfirmation()
    {
        var state = Glory("trial-reconsider");
        var rejected = Choose(state, "reject");
        Assert.Equal(new[] { "accept", "double_down" }, Choices(rejected));
        var confirm = Choose(rejected, "double_down");
        Assert.Equal(new[] { "confirm_abandon", "cancel_abandon" },
            Choices(confirm));
        Assert.Equal(state.Player.Hp, confirm.Player.Hp);
        var cancelled = Choose(confirm, "cancel_abandon");
        Assert.Equal(1, cancelled.World!.Event!.NativePageIndex);
        Assert.Equal(Choices(rejected), Choices(cancelled));
        PrototypeStateInvariants.Validate(cancelled);

        var dead = Choose(confirm.Fork(), "confirm_abandon");
        Assert.Equal(RunPhase.Terminal, dead.Phase);
        Assert.Equal(0, dead.Player.Hp);
        Assert.Equal(CanonicalJson.Sha256(dead),
            CanonicalJson.Sha256(Choose(confirm.Fork(), "confirm_abandon")));
    }

    [Fact]
    public void MerchantGuiltyAddsRegretAndObtainsTwoIndependentRelics()
    {
        var initial = AtPage("merchant-guilty", 2);
        var state = initial with
        {
            World = initial.World! with
            {
                RelicBag = new PrototypeRelicBagState(
                    ["proto.relic.lantern", "proto.relic.anchor"],
                    ["proto.relic.nunchaku", "proto.relic.ornamental_fan"],
                    ["proto.relic.shuriken", "proto.relic.kunai"], [])
            }
        };
        var before = state.Player.Relics.Length;
        var result = Choose(state, "merchant_guilty");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Equal(before + 2, result.Player.Relics.Length);
        Assert.Equal(2, result.Player.Relics.Skip(before)
            .Select(relic => relic.RelicId).Distinct().Count());
        Assert.Single(result.Player.Deck, c =>
            c.CardId == PrototypeNativeGloryEvents.RegretId);
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Choose(state.Fork(), "merchant_guilty"))));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void MerchantInnocentAddsShameThenUpgradesTwoChosenCards()
    {
        var initial = AtPage("merchant-innocent", 2);
        var sourceDeck = initial.Player.Deck;
        var prompted = Choose(initial, "merchant_innocent");
        Assert.Equal(RunPhase.Event, prompted.Phase);
        Assert.Single(prompted.Player.Deck,
            c => c.CardId == PrototypeNativeGloryEvents.ShameId);
        var firstPrompt = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            prompted.World!.Event!.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Upgrade,
            firstPrompt.Kind);
        Assert.Equal(2, firstPrompt.RemainingSelections);
        var id1 = firstPrompt.CandidateCardInstanceIds[0];
        var stage2 = Select(prompted, id1);
        var nextPrompt = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            stage2.World!.Event!.PendingDeckChoice);
        Assert.DoesNotContain(id1, nextPrompt.CandidateCardInstanceIds);
        var id2 = nextPrompt.CandidateCardInstanceIds[0];
        var result = Select(stage2, id2);
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        foreach (var id in new[] { id1, id2 })
            Assert.Equal(sourceDeck.Single(c => c.InstanceId == id).UpgradeLevel + 1,
                result.Player.Deck.Single(c => c.InstanceId == id).UpgradeLevel);
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void NobleBranchesHealOrGrantRegretAndGold()
    {
        var start = AtPage("noble-guilty", 3);
        start = start with
        {
            Player = start.Player with { Hp = start.Player.Hp - 20 }
        };
        var healed = Choose(start, "noble_guilty");
        Assert.Equal(start.Player.Hp + 10, healed.Player.Hp);
        healed = PrototypeEventPageTestHelperV111.ContinueIfFinished(healed);
        Assert.Equal(RunPhase.MapChoice, healed.Phase);
        var innocent = AtPage("noble-innocent", 3);
        var paid = Choose(innocent, "noble_innocent");
        Assert.Equal(innocent.Player.Gold + 300, paid.Player.Gold);
        Assert.Single(paid.Player.Deck, c =>
            c.CardId == PrototypeNativeGloryEvents.RegretId);
        paid = PrototypeEventPageTestHelperV111.ContinueIfFinished(paid);
        Assert.Equal(RunPhase.MapChoice, paid.Phase);
        PrototypeStateInvariants.Validate(healed);
        PrototypeStateInvariants.Validate(paid);
    }

    [Fact]
    public void StrangerGuiltyOffersTwoIndependentOptionalCardRewards()
    {
        var state = AtPage("stranger-guilty", 4);
        var before = state.Player.Deck.Length;
        var result = Choose(state, "nondescript_guilty");
        Assert.Equal(RunPhase.Reward, result.Phase);
        Assert.Equal(before + 1, result.Player.Deck.Length);
        Assert.Equal(PrototypeNativeGloryEvents.DoubtId,
            result.Player.Deck[^1].CardId);
        var reward = Assert.IsType<RewardState>(result.World!.Reward);
        Assert.Equal(2, reward.ExtraCardOptions!.Length);
        Assert.All(reward.ExtraCardOptions, group =>
            Assert.Equal(3, group.Length));
        Assert.Contains(Engine.GetLegalActions(result),
            action => action.Kind == "take_reward_card");

        var first = Step(result, GameAction.Create(
            "take_reward_card", new ChooseCardPayload(0)));
        Assert.Equal(RunPhase.Reward, first.Phase);
        var second = Step(first, GameAction.Empty("skip_reward_card"));
        Assert.Equal(RunPhase.Reward, second.Phase);
        Assert.Equal(new[] { "leave_reward" },
            Engine.GetLegalActions(second).Select(a => a.Kind));
        var completed = Step(second, GameAction.Empty("leave_reward"));
        completed = PrototypeEventPageTestHelperV111.ContinueIfFinished(completed);
        Assert.Equal(RunPhase.MapChoice, completed.Phase);
        Assert.Equal(before + 2, completed.Player.Deck.Length);
        Assert.Equal(CanonicalJson.Sha256(completed),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Step(Step(Step(result.Fork(),
                        GameAction.Create("take_reward_card",
                            new ChooseCardPayload(0))),
                        GameAction.Empty("skip_reward_card")),
                        GameAction.Empty("leave_reward")))));
        PrototypeStateInvariants.Validate(completed);
    }

    [Fact]
    public void StrangerInnocentTransformsTwoPersistentCardIdentities()
    {
        var initial = AtPage("stranger-innocent", 4);
        var prompted = Choose(initial, "nondescript_innocent");
        Assert.Equal(RunPhase.Event, prompted.Phase);
        Assert.Single(prompted.Player.Deck,
            c => c.CardId == PrototypeNativeGloryEvents.DoubtId);
        var ids = new List<long>();
        while (prompted.Phase == RunPhase.Event
               && prompted.World!.Event!.PendingDeckChoice is not null)
        {
            var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
                prompted.World!.Event!.PendingDeckChoice);
            Assert.Equal(PrototypePersistentDeckChoiceKind.Transform,
                pending.Kind);
            var id = pending.CandidateCardInstanceIds[0];
            ids.Add(id);
            prompted = Select(prompted, id);
        }
        prompted = PrototypeEventPageTestHelperV111.ContinueIfFinished(
            prompted);
        Assert.Equal(RunPhase.MapChoice, prompted.Phase);
        Assert.Equal(2, ids.Count);
        Assert.Equal(2, ids.Distinct().Count());
        Assert.All(ids, id =>
            Assert.Contains(prompted.Player.Deck, c => c.InstanceId == id));
        PrototypeStateInvariants.Validate(prompted);
    }

    [Fact]
    public void IllegalCourtActionsLeaveStateUnchanged()
    {
        var start = Glory("trial-invalid");
        foreach (var invalid in new[] { "noble_guilty", "double_down",
            "confirm_abandon" })
        {
            var hash = CanonicalJson.Sha256(start);
            Assert.Throws<InvalidOperationException>(() => Choose(start, invalid));
            Assert.Equal(hash, CanonicalJson.Sha256(start));
        }
        var merchant = AtPage("trial-invalid-merchant", 2);
        Assert.Throws<InvalidOperationException>(() =>
            Choose(merchant, "nondescript_guilty"));
    }

    [Fact]
    public void RegretDealsUnblockableDamageEqualToHandSize()
    {
        var plain = Battle();
        var combat = plain.World!.Combat!;
        var id = combat.NextCardInstanceId;
        var curse = new CombatCardInstance(id, null,
            PrototypeNativeGloryEvents.RegretId, 0, true,
            PrototypeJson.EmptyObject());
        var with = plain with
        {
            World = plain.World with
            {
                Combat = combat with
                {
                    NextCardInstanceId = id + 1,
                    Cards = combat.Cards.Append(curse).ToArray(),
                    Hand = combat.Hand.Append(id).ToArray(),
                    PlayerBlock = 100
                }
            }
        };
        var control = with with
        {
            World = with.World! with
            {
                Combat = with.World.Combat! with
                {
                    Hand = combat.Hand,
                    DiscardPile = combat.DiscardPile.Append(id).ToArray()
                }
            }
        };
        var damaged = Step(with, GameAction.Empty("end_turn"));
        var undamaged = Step(control, GameAction.Empty("end_turn"));
        Assert.Equal(undamaged.Player.Hp - combat.Hand.Length - 1,
            damaged.Player.Hp);
        Assert.Equal(CanonicalJson.Sha256(damaged),
            CanonicalJson.Sha256(Step(with.Fork(),
                GameAction.Empty("end_turn"))));
    }

    [Theory]
    [InlineData(PrototypeNativeGloryEvents.ShameId, "proto.power.frail")]
    [InlineData(PrototypeNativeGloryEvents.DoubtId, "proto.power.weak")]
    public void TrialDebuffCurseSurvivesFirstTurnEnd(
        string cardId, string powerId)
    {
        var state = Battle();
        var combat = state.World!.Combat!;
        var id = combat.NextCardInstanceId;
        var curse = new CombatCardInstance(id, null, cardId, 0, true,
            PrototypeJson.EmptyObject());
        state = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    NextCardInstanceId = id + 1,
                    Cards = combat.Cards.Append(curse).ToArray(),
                    Hand = combat.Hand.Append(id).ToArray(),
                    PlayerBlock = 100
                }
            }
        };
        var firstEnd = Step(state, GameAction.Empty("end_turn"));
        Assert.Equal(RunPhase.Combat, firstEnd.Phase);
        Assert.Contains(firstEnd.World!.Combat!.PlayerPowers,
            power => power.PowerId == powerId && power.Stacks == 1);
        Assert.Equal(CanonicalJson.Sha256(firstEnd),
            CanonicalJson.Sha256(Step(state.Fork(),
                GameAction.Empty("end_turn"))));
    }

    private static RunState Glory(string seed) =>
        PrototypeNativeLaterActFixture.CreateGloryEvent(seed, Trial);

    private static RunState AtPage(string seed, int page)
    {
        var state = Glory(seed);
        return state with
        {
            World = state.World! with
            {
                Event = state.World.Event! with { NativePageIndex = page }
            }
        };
    }

    private static string[] Choices(RunState state) =>
        Engine.GetLegalActions(state)
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId)
            .ToArray();

    private static RunState Choose(RunState state, string choice) =>
        Step(state, GameAction.Create("event_choice",
            new EventChoicePayload(choice)));

    private static RunState Select(RunState state, long id) =>
        Step(state, GameAction.Create("choose_event_deck_card",
            new ChooseEventDeckCardPayload(id)));

    private static RunState Step(RunState state, GameAction action) =>
        Engine.Step(state, action).State;

    private static RunState Battle()
    {
        var original = Step(PrototypeGameFactory.Create(
            "v111-trial-curse-combat"),
            GameAction.Empty("start_run"));
        var state = original with
        {
            Phase = RunPhase.Event,
            World = original.World! with
            {
                Act = 2, Floor = 1,
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
        state = Choose(state, "keep_key");
        state = Choose(state, "fight");
        Assert.Equal(RunPhase.Combat, state.Phase);
        return state;
    }
}
