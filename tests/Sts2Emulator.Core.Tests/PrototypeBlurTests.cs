using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeBlurTests
{
    [Fact]
    public void BlurPreservesBlockForExactlyTheNextPlayerTurn()
    {
        var empty = PrototypeJson.EmptyObject();
        var cards = new[]
        {
            new CombatCardInstance(
                1, 1001, "proto.silent.blur", 0, false, empty),
            new CombatCardInstance(
                2, 1002, "proto.silent.defend", 0, false, empty),
            new CombatCardInstance(
                3, 1003, "proto.silent.defend", 0, false, empty),
            new CombatCardInstance(
                4, 1004, "proto.silent.defend", 0, false, empty),
            new CombatCardInstance(
                5, 1005, "proto.silent.defend", 0, false, empty),
            new CombatCardInstance(
                6, 1006, "proto.silent.defend", 0, false, empty)
        };

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                empty)).ToArray(),
            [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 95,
            Hand: [1],
            DrawPile: [2, 3, 4, 5, 6],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    100,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 7,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        var state = CreateState(player, combat);
        var engine = new PrototypeGameEngine();

        var blur = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, blur).State;

        Assert.Equal(100, state.World!.Combat!.PlayerBlock);
        Assert.Contains(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.blur");

        state = EndTurn(engine, state);

        // Crawler's first 7-damage move reduces 100 -> 93. Blur then
        // prevents the normal player-turn Block clear exactly once.
        Assert.Equal(93, state.World!.Combat!.PlayerBlock);
        Assert.DoesNotContain(
            state.World.Combat.PlayerPowers,
            power => power.PowerId == "proto.power.blur");

        state = EndTurn(engine, state);

        // The next 9-damage enemy move leaves 84 Block, which is then
        // cleared normally because Blur has expired.
        Assert.Equal(0, state.World!.Combat!.PlayerBlock);
    }

    private static RunState EndTurn(
        PrototypeGameEngine engine,
        RunState state) =>
        engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

    private static RunState CreateState(
        PlayerState player,
        CombatState combat)
    {
        var empty = PrototypeJson.EmptyObject();
        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "blur-test",
            "blur-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("blur-test"),
            empty,
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
