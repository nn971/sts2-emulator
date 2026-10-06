using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeGeneratedUpgradeTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void HiddenDaggersGeneratesShivsAtPinnedUpgradeLevel(
        int hiddenDaggersUpgrade,
        int expectedShivUpgrade)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(1, "proto.silent.hidden_daggers", hiddenDaggersUpgrade, empty),
                new CardInstance(2, "proto.silent.strike", 0, empty),
                new CardInstance(3, "proto.silent.defend", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2, 3],
            DrawPile: Array.Empty<long>(),
            DiscardPile: Array.Empty<long>(),
            ExhaustPile: Array.Empty<long>(),
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 4,
            Cards:
            [
                new CombatCardInstance(1, 1, "proto.silent.hidden_daggers", hiddenDaggersUpgrade, false, empty),
                new CombatCardInstance(2, 2, "proto.silent.strike", 0, false, empty),
                new CombatCardInstance(3, 3, "proto.silent.defend", 0, false, empty)
            ],
            PlayerPowers: Array.Empty<PrototypePowerInstanceState>(),
            NextPowerApplicationOrder: 1);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "hidden-daggers",
            "hidden-daggers",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("hidden-daggers"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                4,
                PrototypeRoomType.Combat,
                new MapState(Array.Empty<MapNodeState>()),
                combat,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();
        var play = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);

        state = engine.Step(state, play).State;
        var choose = Assert.Single(engine.GetLegalActions(state));
        Assert.Equal(
            new long[] { 2, 3 },
            choose.ReadPayload<SelectCardsPayload>().CardInstanceIds);

        state = engine.Step(state, choose).State;
        combat = state.World!.Combat!;

        var generatedShivs = combat.Hand
            .Select(id => combat.Cards.Single(card => card.InstanceId == id))
            .Where(card => card.CardId == "proto.silent.shiv")
            .ToArray();

        Assert.Equal(2, generatedShivs.Length);
        Assert.All(
            generatedShivs,
            shiv =>
            {
                Assert.True(shiv.IsTemporary);
                Assert.Equal(expectedShivUpgrade, shiv.UpgradeLevel);
            });
    }
}
