using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeNativeByrdpipTests
{
    private const string Egg = "proto.native.event.byrdonis_egg";
    private const string Swoop = "proto.native.event.byrd_swoop";
    private const string Byrdpip = "proto.native.event.byrdpip";

    private static RunState AtRest(bool hasEgg, bool hasByrdpip = false,
        bool twoEggs = false)
    {
        var empty = PrototypeJson.EmptyObject();
        var deck = new List<CardInstance>
        {
            new(1, "proto.silent.strike", 0, empty)
        };
        if (hasEgg)
        {
            deck.Add(new CardInstance(2, Egg, 0, empty));
            if (twoEggs)
            {
                deck.Add(new CardInstance(3, Egg, 0, empty));
            }
        }

        MapNodeState[] nodes =
        [
            new("r:1", 1, 1, PrototypeRoomType.Combat, ["r:2"]),
            new("r:2", 1, 2, PrototypeRoomType.Combat, ["r:3"]),
            new("r:3", 1, 3, PrototypeRoomType.Combat, ["r:4"]),
            new("r:4", 1, 4, PrototypeRoomType.Combat, ["r:5"]),
            new("r:5", 1, 5, PrototypeRoomType.Rest, ["r:6"]),
            new("r:6", 1, 6, PrototypeRoomType.Boss, [])
        ];
        var player = new PlayerState(35, 70, 99,
            deck.ToArray(),
            hasByrdpip
                ? [new RelicInstance(Byrdpip, empty)]
                : [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);
        var state = new RunState(
            "prototype-unbound", "prototype-0.1",
            "byrdpip-rest", "byrdpip-rest", 0, RunPhase.Rest,
            player, PrototypeRng.CreateBundle("byrdpip-rest"), empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 1, 5,
                hasEgg ? (twoEggs ? 4 : 3) : 2,
                PrototypeRoomType.Rest,
                new MapState(nodes,
                    CurrentNodeId: "r:5", EntryNodeIds: ["r:1"]),
                null, null, null, null, null));
        PrototypeStateInvariants.Validate(state);
        return state;
    }

    [Fact]
    public void HatchAppearsWithEggAndTransformsCardWithoutReplacingIdentity()
    {
        var engine = new PrototypeGameEngine();
        var state = AtRest(hasEgg: true);
        Assert.Contains(engine.GetLegalActions(state),
            action => action.Kind == "rest_hatch");
        var eggId = state.Player.Deck[1].InstanceId;
        var originalNextId = state.World!.NextCardInstanceId;

        var fork = state.Fork();
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(fork));
        state = engine.Step(state, GameAction.Empty("rest_hatch")).State;

        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(35, state.Player.Hp);
        Assert.Equal(70, state.Player.MaxHp);
        Assert.Single(state.Player.Relics,
            relic => relic.RelicId == Byrdpip);
        Assert.Single(state.Player.Deck,
            card => card.InstanceId == eggId && card.CardId == Swoop);
        Assert.DoesNotContain(state.Player.Deck, card => card.CardId == Egg);
        Assert.Equal(originalNextId, state.World!.NextCardInstanceId);
        Assert.Single(state.World.CompletedRooms,
            room => room.RoomType == PrototypeRoomType.Rest);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void HatchConvertsEveryEggAndOnlyAddsOneRelic()
    {
        var engine = new PrototypeGameEngine();
        var state = AtRest(hasEgg: true, twoEggs: true);
        var eggIds = state.Player.Deck
            .Where(card => card.CardId == Egg)
            .Select(card => card.InstanceId).ToArray();

        state = engine.Step(state, GameAction.Empty("rest_hatch")).State;

        Assert.Single(state.Player.Relics,
            relic => relic.RelicId == Byrdpip);
        Assert.Equal(eggIds, state.Player.Deck
            .Where(card => card.CardId == Swoop)
            .Select(card => card.InstanceId));
        Assert.All(state.Player.Deck.Where(card => card.CardId == Swoop),
            card => Assert.Equal(0, card.UpgradeLevel));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void HatchOptionRequiresEggAndAvailableEventPet()
    {
        var engine = new PrototypeGameEngine();
        var noEgg = AtRest(hasEgg: false);
        Assert.DoesNotContain(engine.GetLegalActions(noEgg),
            action => action.Kind == "rest_hatch");
        Assert.Throws<InvalidOperationException>(
            () => engine.Step(noEgg, GameAction.Empty("rest_hatch")));

        var alreadyHasPet = AtRest(hasEgg: true, hasByrdpip: true);
        Assert.DoesNotContain(engine.GetLegalActions(alreadyHasPet),
            action => action.Kind == "rest_hatch");
        Assert.Throws<InvalidOperationException>(
            () => engine.Step(alreadyHasPet, GameAction.Empty("rest_hatch")));
        PrototypeStateInvariants.Validate(alreadyHasPet);
    }

    [Fact]
    public void ByrdonisNestEligibilityCoversUnhatchedAndHatchedPet()
    {
        var nest = PrototypeContent.Event("proto.native.event.byrdonis_nest");
        var empty = AtRest(hasEgg: false).Player;
        var egg = AtRest(hasEgg: true).Player;
        var pet = AtRest(hasEgg: false, hasByrdpip: true).Player;

        Assert.True(PrototypeNativeOvergrowthEvents.IsEligible(nest, empty));
        Assert.False(PrototypeNativeOvergrowthEvents.IsEligible(nest, egg));
        Assert.False(PrototypeNativeOvergrowthEvents.IsEligible(nest, pet));
    }

    [Fact]
    public void ByrdSwoopHasNativeZeroCostAndUpgradeDamage()
    {
        var card = PrototypeContent.Card(Swoop);
        Assert.Equal(PrototypeCardType.Attack, card.Type);
        Assert.Equal(PrototypeCardTarget.Enemy, card.Target);
        Assert.Equal(0, card.Cost.Amount);
        Assert.False(card.RewardEligible);
        var damage = Assert.Single(card.Effects);
        Assert.Equal(PrototypeCombatEffectKind.DamageEnemy, damage.Kind);
        Assert.Equal(14, damage.Amount);
        Assert.Equal(4, damage.UpgradeDelta);
    }
    [Theory]
    [InlineData("proto.native.event.byrdonis_egg")]
    [InlineData("proto.native.event.spore_mind")]
    [InlineData("proto.native.event.poor_sleep")]
    public void RestSiteNeverOffersUpgradeForNativeZeroUpgradeCard(string cardId)
    {
        var state = AtRest(hasEgg: true);
        state = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Select(card =>
                    card.InstanceId == 2
                        ? card with { CardId = cardId }
                        : card).ToArray()
            }
        };
        PrototypeStateInvariants.Validate(state);
        var actions = new PrototypeGameEngine().GetLegalActions(state);
        Assert.DoesNotContain(actions, action =>
            action.Kind == "rest_upgrade"
            && action.ReadPayload<UpgradeCardPayload>().CardInstanceId == 2);
        Assert.Contains(actions, action => action.Kind == "rest_upgrade"
            && action.ReadPayload<UpgradeCardPayload>().CardInstanceId == 1);
    }

}
