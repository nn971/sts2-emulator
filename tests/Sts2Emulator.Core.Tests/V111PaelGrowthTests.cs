using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111PaelGrowthTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void GrowthAcquisitionOffersOneEligibleCardAndPersistsCloneAmountFour()
    {
        var state = NativePaelRewardsV111.CreateRewardFixture("growth-enchant", NativePaelRewardsV111.GrowthId);
        var acquire = Assert.Single(Engine.GetLegalActions(state));
        var pending = Engine.Step(state, acquire).State;
        var choice = pending.World!.Event!.PendingDeckChoice!;
        Assert.Equal(PrototypePersistentDeckChoiceKind.Enchant, choice.Kind);
        Assert.Equal(1, choice.RemainingSelections);
        Assert.NotEmpty(choice.CandidateCardInstanceIds);
        Assert.All(choice.CandidateCardInstanceIds, id => Assert.Null(
            pending.Player.Deck.Single(card => card.InstanceId == id).Enchantment));
        var pick = Engine.GetLegalActions(pending).First(action => action.Kind == "choose_event_deck_card");
        var selected = pick.ReadPayload<ChooseDeckCardPayload>().CardInstanceId;
        var result = Engine.Step(RunSnapshot.Load(RunSnapshot.Save(pending)), pick).State;
        var enchanted = result.Player.Deck.Single(card => card.InstanceId == selected);
        Assert.Equal(new PrototypeCardEnchantment(PrototypeCardEnchantmentKind.Clone, 4), enchanted.Enchantment);
        Assert.Equal(CanonicalJson.Sha256(result), CanonicalJson.Sha256(
            Engine.Step(pending, pick).State));
        PrototypeStateInvariants.Validate(result);

        var malformed = pending with { World = pending.World with
        { Event = pending.World.Event with { PendingDeckChoice = choice with
            { EnchantmentKind = PrototypeCardEnchantmentKind.Goopy } } } };
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(malformed));
    }

    [Fact]
    public void CloneRestCopiesEveryMarkedCardWithPersistentIdentityAndEnchantment()
    {
        var state = PrototypeGameFactory.Create("growth-rest-clone");
        state = Engine.Step(state, Assert.Single(Engine.GetLegalActions(state))).State;
        var world = state.World!;
        var id = world.NextCardInstanceId;
        var original = state.Player.Deck.First(card =>
            PrototypeContent.Card(card.CardId).Tags?.Contains("Defend") == true);
        original = original with { Enchantment = new(PrototypeCardEnchantmentKind.Clone, 4) };
        state = state with
        {
            Player = state.Player with
            {
                Relics = [..state.Player.Relics, new(NativePaelRewardsV111.GrowthId, PrototypeJson.EmptyObject())],
                Deck = [..state.Player.Deck, original with { InstanceId = id }]
            },
            World = world with
            {
                Floor = 3, ActiveRoom = null,
                NextCardInstanceId = id + 1,
                Map = RestReachableMap()
            }
        };
        state = state with { Phase = RunPhase.MapChoice };
        var restNode = state.World!.Map.AvailableNodes().Single(node => node.RoomType == PrototypeRoomType.Rest);
        state = Engine.Step(state, GameAction.Create("choose_map_node",
            new ChooseMapNodePayload(restNode.NodeId))).State;
        Assert.Equal(RunPhase.Rest, state.Phase);
        Assert.Contains(Engine.GetLegalActions(state), action => action.Kind == "rest_clone");
        var beforeDeck = state.Player.Deck.Length;
        var beforeMaxId = state.Player.Deck.Max(card => card.InstanceId);
        var action = GameAction.Empty("rest_clone");
        var cloned = Engine.Step(state, action).State;
        Assert.Equal(CanonicalJson.Sha256(cloned), CanonicalJson.Sha256(
            Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
        var copies = cloned.Player.Deck.Where(card => card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Clone).ToArray();
        Assert.Equal(2, copies.Length);
        Assert.Equal(new PrototypeCardEnchantment(PrototypeCardEnchantmentKind.Clone, 4), copies[^1].Enchantment);
        Assert.True(copies[^1].InstanceId > beforeMaxId);
        Assert.Equal(beforeDeck + 1, cloned.Player.Deck.Length);
        Assert.Equal(id + 2, cloned.World!.NextCardInstanceId);
        Assert.Equal(RunPhase.MapChoice, cloned.Phase);
        PrototypeStateInvariants.Validate(cloned);
    }

    private static MapState RestReachableMap()
    {
        var nodes = new List<MapNodeState>();
        void Add(string id, int floor, PrototypeRoomType room, params string[] next) =>
            nodes.Add(new(id, 1, floor, room, next));
        Add("f1a", 1, PrototypeRoomType.Combat, "f2event");
        Add("f1b", 1, PrototypeRoomType.Combat, "f2shop");
        Add("f1c", 1, PrototypeRoomType.Combat, "f2combat");
        Add("f2event", 2, PrototypeRoomType.Event, "f3combat");
        Add("f2shop", 2, PrototypeRoomType.Shop, "f3combat");
        Add("f2combat", 2, PrototypeRoomType.Combat, "f3event", "f3elite");
        Add("f3event", 3, PrototypeRoomType.Event, "f4shop", "f4rest");
        Add("f3elite", 3, PrototypeRoomType.Elite, "f4combat");
        Add("f3combat", 3, PrototypeRoomType.Combat, "f4shop", "f4rest");
        Add("f4shop", 4, PrototypeRoomType.Shop, "f5a");
        Add("f4rest", 4, PrototypeRoomType.Rest, "f5a", "f5b");
        Add("f4combat", 4, PrototypeRoomType.Combat, "f5b");
        Add("f5a", 5, PrototypeRoomType.Rest, "boss");
        Add("f5b", 5, PrototypeRoomType.Rest, "boss");
        Add("boss", 6, PrototypeRoomType.Boss);
        return new(nodes.ToArray(), CurrentNodeId: "f3combat",
            EntryNodeIds: ["f1a", "f1b", "f1c"]);
    }
}
