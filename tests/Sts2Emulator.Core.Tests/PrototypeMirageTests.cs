using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeMirageTests
{
    [Fact]
    public void MirageDefinitionMatchesPinnedV01110Semantics()
    {
        var card = PrototypeContent.Card("proto.silent.mirage");

        Assert.Equal(1, card.Cost.AmountAt(0));
        Assert.Equal(1, card.Cost.AmountAt(1));
        Assert.Equal(PrototypeCardType.Skill, card.Type);
        Assert.Equal(PrototypeCardRarity.Uncommon, card.Rarity);
        Assert.True(card.ExhaustOnUse);
        Assert.True(card.LoseExhaustOnUpgrade);

        var effect = Assert.Single(card.Effects);
        Assert.Equal(
            PrototypeCombatEffectKind.GainPlayerBlockFromEnemyStatusTotal,
            effect.Kind);
        Assert.Equal("proto.status.poison", effect.StatusId);
    }

    [Fact]
    public void MirageGainsBlockFromTotalPoisonOnLivingEnemies()
    {
        var mirage = Card(1, "proto.silent.mirage");
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            mirage,
            [
                Enemy(1, hp: 20, poison: 5),
                Enemy(2, hp: 10, poison: 3),
                Enemy(3, hp: 0, poison: 99)
            ]);

        state = PlayCard(engine, state, mirage.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(8, combat.PlayerBlock);
        Assert.Equal(2, combat.Energy);
        Assert.Contains(mirage.InstanceId, combat.ExhaustPile);
        Assert.DoesNotContain(mirage.InstanceId, combat.DiscardPile);
    }

    [Fact]
    public void UpgradedMirageLosesExhaust()
    {
        var mirage = Card(
            1,
            "proto.silent.mirage",
            upgradeLevel: 1);
        var engine = new PrototypeGameEngine();
        var state = CreateState(
            mirage,
            [Enemy(1, hp: 20, poison: 4)]);

        state = PlayCard(engine, state, mirage.InstanceId);

        var combat = state.World!.Combat!;
        Assert.Equal(4, combat.PlayerBlock);
        Assert.Contains(mirage.InstanceId, combat.DiscardPile);
        Assert.DoesNotContain(mirage.InstanceId, combat.ExhaustPile);
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
        int upgradeLevel = 0) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            upgradeLevel,
            false,
            PrototypeJson.EmptyObject());

    private static EnemyCombatState Enemy(
        int instanceId,
        int hp,
        int poison)
    {
        var statuses = new Dictionary<string, int>(
            StringComparer.Ordinal);
        if (poison > 0)
        {
            statuses["proto.status.poison"] = poison;
        }

        return new EnemyCombatState(
            instanceId,
            "proto.enemy.crawler",
            hp,
            0,
            0,
            statuses);
    }

    private static RunState CreateState(
        CombatCardInstance card,
        EnemyCombatState[] enemies)
    {
        var empty = PrototypeJson.EmptyObject();
        var player = new PlayerState(
            70,
            70,
            0,
            [
                new CardInstance(
                    card.PersistentCardInstanceId!.Value,
                    card.CardId,
                    card.UpgradeLevel,
                    card.State.Clone())
            ],
            Array.Empty<RelicInstance>(),
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [card.InstanceId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: enemies,
            NextCardInstanceId: card.InstanceId + 1,
            Cards: [card],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "mirage-test",
            "mirage-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle("mirage-test"),
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
