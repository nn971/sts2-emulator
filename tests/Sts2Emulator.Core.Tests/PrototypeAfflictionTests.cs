using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeAfflictionTests
{
    [Fact]
    public void HexedAddsEffectiveEtherealAtEndOfTurn()
    {
        var hexed = Card(
            1,
            "proto.silent.defend") with
        {
            Affliction = new PrototypeCardAffliction(
                PrototypeCardAfflictionKind.Hexed,
                1)
        };
        var drawPile = Enumerable.Range(2, 5)
            .Select(id => Card(id, "proto.silent.strike"))
            .ToArray();

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [hexed],
            drawPile: drawPile);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Contains(
            hexed.InstanceId,
            combat.ExhaustPile);
        Assert.DoesNotContain(
            hexed.InstanceId,
            combat.DiscardPile);
        Assert.DoesNotContain(
            hexed.InstanceId,
            combat.Hand);

        var card = combat.Cards.Single(
            item => item.InstanceId == hexed.InstanceId);
        Assert.NotNull(card.Affliction);
        Assert.Equal(
            PrototypeCardAfflictionKind.Hexed,
            card.Affliction!.Kind);
    }

    [Fact]
    public void NonHexedAfflictionDoesNotAddEthereal()
    {
        var entangled = Card(
            1,
            "proto.silent.defend") with
        {
            Affliction = new PrototypeCardAffliction(
                PrototypeCardAfflictionKind.Entangled,
                1)
        };
        var drawPile = Enumerable.Range(2, 5)
            .Select(id => Card(id, "proto.silent.strike"))
            .ToArray();

        var engine = new PrototypeGameEngine();
        var state = CreateState(
            hand: [entangled],
            drawPile: drawPile);

        state = engine.Step(
            state,
            engine.GetLegalActions(state)
                .Single(action => action.Kind == "end_turn")).State;

        var combat = state.World!.Combat!;
        Assert.Contains(
            entangled.InstanceId,
            combat.DiscardPile);
        Assert.DoesNotContain(
            entangled.InstanceId,
            combat.ExhaustPile);
    }

    private static CombatCardInstance Card(
        long instanceId,
        string cardId) =>
        new(
            instanceId,
            1000 + instanceId,
            cardId,
            0,
            false,
            PrototypeJson.EmptyObject());

    private static RunState CreateState(
        CombatCardInstance[] hand,
        CombatCardInstance[] drawPile)
    {
        var cards = hand.Concat(drawPile).ToArray();
        var empty = PrototypeJson.EmptyObject();

        var player = new PlayerState(
            70,
            70,
            0,
            cards.Select(card => new CardInstance(
                card.PersistentCardInstanceId!.Value,
                card.CardId,
                card.UpgradeLevel,
                card.State.Clone())).ToArray(),
            Array.Empty<RelicInstance>(),
            new PotionInstance?[
                PrototypeContent.Rules.PotionSlots]);

        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: hand.Select(
                card => card.InstanceId).ToArray(),
            DrawPile: drawPile.Select(
                card => card.InstanceId).ToArray(),
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
                cards.Max(card => card.InstanceId) + 1,
            Cards: cards,
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);

        return new RunState(
            "prototype-unbound",
            "prototype-0.1",
            "affliction-test",
            "affliction-test",
            0,
            RunPhase.Combat,
            player,
            PrototypeRng.CreateBundle(
                "affliction-test"),
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
