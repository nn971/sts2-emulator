using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeAttackModifierTests
{
    [Fact]
    public void VulnerableOnlyAmplifiesPoweredAttackDamage()
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(100, "proto.silent.assassinate", 0, empty),
                new CardInstance(101, "proto.silent.strike", 0, empty)
            ],
            Array.Empty<RelicInstance>(),
            [
                new PotionInstance("proto.potion.fire", empty),
                null
            ]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [1, 2],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    50,
                    0,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: 3,
            Cards:
            [
                new CombatCardInstance(1, 100, "proto.silent.assassinate", 0, false, empty),
                new CombatCardInstance(2, 101, "proto.silent.strike", 0, false, empty)
            ],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1,
            Potions:
            [
                new CombatPotionState(0, "proto.potion.fire", empty)
            ]);

        var state = new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "attack-modifier-test",
            "attack-modifier-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("attack-modifier-test"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId,
                PrototypeContent.CharacterId,
                1,
                1,
                102,
                PrototypeRoomType.Combat,
                new MapState([]),
                combat,
                null,
                null,
                null,
                null));

        var engine = new PrototypeGameEngine();

        var assassinate = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 1);
        state = engine.Step(state, assassinate).State;

        var enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(40, enemy.Hp);
        Assert.Equal(1, enemy.Statuses["proto.status.vulnerable"]);

        var firePotion = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "use_potion"
                && action.ReadPayload<UsePotionPayload>().Slot == 0);
        state = engine.Step(state, firePotion).State;

        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(20, enemy.Hp);

        var strike = engine.GetLegalActions(state)
            .Single(action =>
                action.Kind == "play_card"
                && action.ReadPayload<PlayCardPayload>().CardInstanceId == 2);
        state = engine.Step(state, strike).State;

        enemy = Assert.Single(state.World!.Combat!.Enemies);
        Assert.Equal(11, enemy.Hp);
        Assert.Equal(1, enemy.Statuses["proto.status.vulnerable"]);
    }
}
