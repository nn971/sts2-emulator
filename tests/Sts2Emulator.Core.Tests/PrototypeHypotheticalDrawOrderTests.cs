using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypeHypotheticalDrawOrderTests
{
    private static (RunState State, PrototypeAiEnvironment Environment)
        FreshCombat(string seed)
    {
        var environment = new PrototypeAiEnvironment();
        var engine = new PrototypeGameEngine();
        var state = environment.Reset(seed);
        state = engine.Step(state, Assert.Single(engine.GetLegalActions(state))).State;
        var entry = engine.GetLegalActions(state)
            .First(action =>
            {
                if (action.Kind != "choose_map_node")
                {
                    return false;
                }
                var target = action.ReadPayload<ChooseMapNodePayload>();
                return state.World!.Map.AvailableNodes()
                    .Single(node => node.NodeId == target.NodeId)
                    .RoomType == PrototypeRoomType.Combat;
            });
        state = engine.Step(state, entry).State;
        Assert.Equal(RunPhase.Combat, state.Phase);
        Assert.NotEmpty(state.World!.Combat!.DrawPile);
        return (state, environment);
    }

    private static PrototypePublicDrawCard Variant(CombatCardInstance card) =>
        new(card.CardId, card.UpgradeLevel);

    private static PrototypePublicDrawCard[] PublicRemainingOrder(RunState state)
    {
        // Intentionally derive identities from public deck and visible hand,
        // NOT from the private current DrawPile order.
        var combat = state.World!.Combat!;
        var cardsById = combat.Cards.ToDictionary(card => card.InstanceId);
        var publicDeck = state.Player.Deck
            .Select(card => new PrototypePublicDrawCard(card.CardId, card.UpgradeLevel))
            .ToList();
        foreach (var id in combat.Hand)
        {
            Assert.True(publicDeck.Remove(Variant(cardsById[id])));
        }
        Assert.Equal(combat.DrawPile.Length, publicDeck.Count);
        return publicDeck
            .OrderBy(card => card.CardId, StringComparer.Ordinal)
            .ThenBy(card => card.UpgradeLevel)
            .ToArray();
    }

    [Fact]
    public void ReorderChangesOnlyPrivateDrawOrder_AndLeavesSourceImmutable()
    {
        var (original, environment) = FreshCombat("hypothetical-order-test");
        var sourceHash = CanonicalJson.Sha256(original);
        var publicObservation = environment.Observe(original);
        var order = PublicRemainingOrder(original);
        var modified = PrototypeHypotheticalDrawOrder.Apply(original, order);

        Assert.Equal(sourceHash, CanonicalJson.Sha256(original));
        Assert.Equal(publicObservation.ObservationHash,
            environment.Observe(modified).ObservationHash);
        Assert.Equal(
            publicObservation.LegalActions.Select(action => action.ActionId),
            environment.Observe(modified).LegalActions.Select(action => action.ActionId));
        Assert.Equal(
            CanonicalJson.Sha256(original.Rng),
            CanonicalJson.Sha256(modified.Rng));
        Assert.Equal(original.DecisionIndex, modified.DecisionIndex);
        Assert.Equal(PrototypeHypotheticalDrawOrder.SchemaId,
            "prototype-hypothetical-draw-order-v1");

        var byId = modified.World!.Combat!.Cards
            .ToDictionary(card => card.InstanceId);
        var actualFirstDrawn = modified.World.Combat.DrawPile
            .Reverse()
            .Select(id => Variant(byId[id]))
            .ToArray();
        Assert.Equal(order, actualFirstDrawn);
        Assert.Equal(
            original.World!.Combat!.Hand,
            modified.World.Combat.Hand);
    }

    [Fact]
    public void DistinctFutureOrdersLeadToValidCombatSuccessors()
    {
        var (state, environment) = FreshCombat("hypothetical-successors");
        var order = PublicRemainingOrder(state);
        var reversal = order.Reverse().ToArray();
        var first = PrototypeHypotheticalDrawOrder.Apply(state, order);
        var second = PrototypeHypotheticalDrawOrder.Apply(state, reversal);
        var engine = new PrototypeGameEngine();
        var endTurn = environment.Observe(first).LegalActions
            .Single(action => action.Kind == "end_turn");

        var firstNext = environment.Step(first, endTurn.ActionId).State;
        var secondNext = environment.Step(second, endTurn.ActionId).State;
        Assert.Equal(firstNext.Phase, secondNext.Phase);
        Assert.True(firstNext.Player.Hp > 0);
        Assert.True(secondNext.Player.Hp > 0);
        Assert.Equal(
            firstNext.World!.Combat!.Hand.Length,
            secondNext.World!.Combat!.Hand.Length);
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(FreshCombat("hypothetical-successors").State));
        Assert.NotNull(engine.GetLegalActions(firstNext));
        Assert.NotNull(engine.GetLegalActions(secondNext));
    }

    [Fact]
    public void InvalidPermutationAndUncertifiedCombatFailClosed()
    {
        var (state, _) = FreshCombat("hypothetical-invalid");
        var order = PublicRemainingOrder(state);
        var sourceHash = CanonicalJson.Sha256(state);

        Assert.Throws<ArgumentException>(() =>
            PrototypeHypotheticalDrawOrder.Apply(state, order[..^1]));
        var impossible = order.ToArray();
        impossible[0] = new PrototypePublicDrawCard("not-in-pile", 0);
        Assert.Throws<ArgumentException>(() =>
            PrototypeHypotheticalDrawOrder.Apply(state, impossible));
        var wrongUpgrade = order.ToArray();
        wrongUpgrade[0] = wrongUpgrade[0] with { UpgradeLevel = 99 };
        Assert.Throws<ArgumentException>(() =>
            PrototypeHypotheticalDrawOrder.Apply(state, wrongUpgrade));

        var modified = state.Fork();
        modified = modified with
        {
            World = modified.World! with
            {
                Combat = modified.World.Combat! with { Turn = 2 }
            }
        };
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeHypotheticalDrawOrder.Apply(modified, order));
        Assert.Throws<InvalidOperationException>(() =>
            PrototypeHypotheticalDrawOrder.Apply(
                new PrototypeAiEnvironment().Reset("not-combat"), order));
        Assert.Equal(sourceHash, CanonicalJson.Sha256(state));
    }
}
