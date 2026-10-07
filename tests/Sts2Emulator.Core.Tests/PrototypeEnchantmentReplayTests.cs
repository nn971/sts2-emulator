using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeEnchantmentReplayTests
{
    [Fact]
    public void SpiralAddsOneWholeCardReplay()
    {
        var defend = Card(
            1,
            "proto.silent.defend",
            enchantment: new(
                PrototypeCardEnchantmentKind.Spiral));

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [defend]);

        state = PlayCard(
            engine,
            state,
            defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(10, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Equal(
            2,
            combat.CounterState.SkillsPlayedThisTurn);
        Assert.Equal(
            1,
            combat.DiscardPile.Count(
                id => id == defend.InstanceId));

        var card = combat.Cards.Single(
            item => item.InstanceId == defend.InstanceId);
        Assert.NotNull(card.Enchantment);
        Assert.Equal(
            PrototypeCardEnchantmentKind.Spiral,
            card.Enchantment!.Kind);
        Assert.False(card.EnchantmentTriggeredThisCombat);
    }

    [Fact]
    public void SpiralAndBurstAddTheirReplayCounts()
    {
        var defend = Card(
            1,
            "proto.silent.defend",
            enchantment: new(
                PrototypeCardEnchantmentKind.Spiral));

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [defend],
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.burst",
                    1,
                    1)
            ]);

        state = PlayCard(
            engine,
            state,
            defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(15, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Equal(
            3,
            combat.CounterState.SkillsPlayedThisTurn);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.burst");
        Assert.Equal(
            1,
            combat.DiscardPile.Count(
                id => id == defend.InstanceId));
    }

    [Fact]
    public void GlamReplaysOnlyTheFirstPlayEachCombat()
    {
        var defend = Card(
            1,
            "proto.silent.defend",
            enchantment: new(
                PrototypeCardEnchantmentKind.Glam));

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [defend]);

        state = PlayCard(
            engine,
            state,
            defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(10, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Equal(
            2,
            combat.CounterState.SkillsPlayedThisTurn);

        var afterFirstPlay = combat.Cards.Single(
            item => item.InstanceId == defend.InstanceId);
        Assert.True(
            afterFirstPlay.EnchantmentTriggeredThisCombat);

        // Put the same combat-card instance back in Hand. The Glam
        // consumption bit belongs to the card instance rather than its
        // current zone, so a second play in the same combat is ordinary.
        combat = combat with
        {
            Hand = [defend.InstanceId],
            DiscardPile = combat.DiscardPile
                .Where(id => id != defend.InstanceId)
                .ToArray()
        };
        state = state with
        {
            World = state.World! with { Combat = combat }
        };

        state = PlayCard(
            engine,
            state,
            defend.InstanceId);

        combat = state.World!.Combat!;
        Assert.Equal(15, combat.PlayerBlock);
        Assert.Equal(1, combat.Energy);
        Assert.Equal(
            3,
            combat.CounterState.SkillsPlayedThisTurn);
        Assert.True(
            combat.Cards.Single(
                    item => item.InstanceId == defend.InstanceId)
                .EnchantmentTriggeredThisCombat);
        Assert.Equal(
            1,
            combat.DiscardPile.Count(
                id => id == defend.InstanceId));
    }

    [Fact]
    public void GlamAndBurstStackOnlyOnTheFirstPlay()
    {
        var defend = Card(
            1,
            "proto.silent.defend",
            enchantment: new(
                PrototypeCardEnchantmentKind.Glam));

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            energy: 3,
            hand: [defend],
            powers:
            [
                new PrototypePowerInstanceState(
                    "proto.power.burst",
                    1,
                    1)
            ]);

        state = PlayCard(
            engine,
            state,
            defend.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(15, combat.PlayerBlock);
        Assert.Equal(
            3,
            combat.CounterState.SkillsPlayedThisTurn);
        Assert.True(
            combat.Cards.Single(
                    item => item.InstanceId == defend.InstanceId)
                .EnchantmentTriggeredThisCombat);
        Assert.DoesNotContain(
            combat.PlayerPowers,
            power => power.PowerId == "proto.power.burst");
    }

    private static RunState PlayCard(
        PrototypeGameEngine engine,
        RunState state,
        long cardInstanceId)
    {
        var action = engine.GetLegalActions(state)
            .Single(item =>
                item.Kind == "play_card"
                && item.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == cardInstanceId);
        return engine.Step(state, action).State;
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId,
        PrototypeCardEnchantment? enchantment = null) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject(),
            Enchantment: enchantment);

    private static RunState CreateState(
        int energy,
        CombatCardInstance[] hand,
        PrototypePowerInstanceState[]? powers = null)
    {
        powers ??= [];
        var empty = PrototypeJson.EmptyObject();

        var player = new PlayerState(
            70,
            70,
            0,
            hand.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                card.State.Clone(),
                card.Enchantment)).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: energy,
            PlayerBlock: 0,
            Hand: hand.Select(
                card => card.InstanceId).ToArray(),
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies:
            [
                new EnemyCombatState(
                    1,
                    "proto.enemy.crawler",
                    999,
                    0,
                    0,
                    new Dictionary<string, int>(
                        StringComparer.Ordinal))
            ],
            NextCardInstanceId:
                hand.Max(card => card.InstanceId) + 1,
            Cards: hand,
            PlayerPowers: powers,
            NextPowerApplicationOrder:
                powers.Length == 0
                    ? 1
                    : powers.Max(
                        power => power.ApplicationOrder) + 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "enchantment-replay-test",
            "enchantment-replay-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "enchantment-replay-test"),
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
