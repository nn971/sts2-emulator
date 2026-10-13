using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeShopTests
{
    [Fact]
    public void CardRemovalIsEnumeratedPerCardAndCanBeUsedOnce()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            Hp: 70,
            MaxHp: 70,
            Gold: 100,
            Deck:
            [
                new CardInstance(1, "proto.silent.strike", 0, empty),
                new CardInstance(2, "proto.silent.defend", 0, empty)
            ],
            Relics: Array.Empty<RelicInstance>(),
            PotionSlots: new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "shop-remove-test",
            "shop-remove-test",
            0,
            RunPhase.Shop,
            player,
            PrototypeRng.CreateBundle("shop-remove-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                3,
                PrototypeRoomType.Shop,
                new MapState(Array.Empty<MapNodeState>()),
                null,
                null,
                new ShopState(
                    Array.Empty<ShopOffer>(),
                    null,
                    null,
                    RemovalPrice: 75),
                null,
                null));

        var engine = new PrototypeGameEngine();
        PrototypeStateInvariants.Validate(state);

        var removeActions = engine.GetLegalActions(state)
            .Where(action => action.Kind == "remove_card")
            .ToArray();
        Assert.Equal(2, removeActions.Length);

        var removeStrike = removeActions.Single(action =>
            action.ReadPayload<RemoveCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, removeStrike).State;
        PrototypeStateInvariants.Validate(state);

        Assert.Equal(25, state.Player.Gold);
        Assert.Equal(
            new long[] { 2 },
            state.Player.Deck.Select(card => card.InstanceId).ToArray());
        Assert.True(state.World!.Shop!.RemovalUsed);
        Assert.DoesNotContain(
            engine.GetLegalActions(state),
            action => action.Kind == "remove_card");
    }
}
