using System.Reflection;
using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111LaterActV3CombatTests
{
    [Fact]
    public void BigMushroomSubtractsTwoFromTheActualOpeningHand()
    {
        var deck = PrototypeContent.StartingDeck.Take(10)
            .Select((id, i) => new CardInstance(i + 1, id, 0,
                PrototypeJson.EmptyObject())).ToArray();
        Assert.True(deck.Length >= 7);
        var control = Start("big-mushroom-draw", deck, []);
        var mushroom = Start("big-mushroom-draw", deck,
            [PrototypeNativeLaterActEventExpansionV3.BigMushroomId]);
        Assert.Equal(control.World!.Combat!.Hand.Length - 2,
            mushroom.World!.Combat!.Hand.Length);
        Assert.Equal(control.Player.Hp, mushroom.Player.Hp);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 5)]
    public void MetamorphosisGeneratesFreeForCombatAttacksIntoDraw(
        int upgrade, int expected)
    {
        var source = new CardInstance(1,
            PrototypeNativeLaterActEventExpansionV3.MetamorphosisId,
            upgrade, PrototypeJson.EmptyObject());
        var engine = new PrototypeGameEngine();
        var state = Start("metamorphosis-" + upgrade, [source], []);
        var sourceCombat = Assert.Single(state.World!.Combat!.Hand);
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card" &&
            a.ReadPayload<PlayCardPayload>().CardInstanceId
                == sourceCombat);
        var after = engine.Step(state, action).State;
        var generated = after.World!.Combat!.Cards.Where(c =>
            c.IsTemporary && c.PersistentCardInstanceId is null
            && c.CardId != PrototypeNativeLaterActEventExpansionV3.MetamorphosisId)
            .ToArray();
        Assert.Equal(expected, generated.Length);
        Assert.All(generated, card =>
        {
            Assert.Equal(PrototypeCardType.Attack,
                PrototypeContent.Card(card.CardId).Type);
            Assert.Equal(0, card.TemporaryEnergyCost?.Cost);
            Assert.Equal(PrototypeTemporaryCardCostExpiry.None,
                card.TemporaryEnergyCost?.Expiry);
            Assert.Contains(card.InstanceId, after.World.Combat.DrawPile);
        });
        Assert.Contains(sourceCombat, after.World.Combat.ExhaustPile);
        Assert.DoesNotContain(generated.Select(c => c.InstanceId),
            id => after.World.Combat.Hand.Contains(id));
    }

    [Theory]
    [InlineData(0, PrototypeTemporaryCardCostExpiry.EndOfTurn)]
    [InlineData(1, PrototypeTemporaryCardCostExpiry.None)]
    public void EnlightenmentReducesHandCostsWithoutIncreasingFreeCards(
        int upgrade, PrototypeTemporaryCardCostExpiry expectedExpiry)
    {
        var expensiveId = PrototypeContent.NativeSilentCardPool.First(id =>
        {
            var def = PrototypeContent.Card(id);
            return def.MechanicsImplemented
                && def.Cost.Kind == PrototypeCardCostKind.Fixed
                && def.Cost.Amount >= 2
                && def.Type == PrototypeCardType.Skill;
        });
        var deck = new[]
        {
            new CardInstance(1,
                PrototypeNativeLaterActEventExpansionV3.EnlightenmentId,
                upgrade, PrototypeJson.EmptyObject()),
            new CardInstance(2, expensiveId, 0, PrototypeJson.EmptyObject()),
            new CardInstance(3, "proto.silent.defend", 0,
                PrototypeJson.EmptyObject())
        };
        var engine = new PrototypeGameEngine();
        var state = Start("enlightenment-" + upgrade, deck, []);
        var combat = state.World!.Combat!;
        var enlightenment = combat.Hand.Single(id => combat.Cards.Single(c =>
            c.InstanceId == id).CardId ==
            PrototypeNativeLaterActEventExpansionV3.EnlightenmentId);
        var action = engine.GetLegalActions(state).Single(a =>
            a.Kind == "play_card" &&
            a.ReadPayload<PlayCardPayload>().CardInstanceId == enlightenment);
        state = engine.Step(state, action).State;
        combat = state.World!.Combat!;
        var expensive = combat.Cards.Single(c => c.CardId == expensiveId);
        Assert.Equal(1, expensive.TemporaryEnergyCost?.Cost);
        var expected = upgrade == 0
            ? PrototypeTemporaryCardCostExpiry.EndOfTurn
                | PrototypeTemporaryCardCostExpiry.WhenPlayed
            : expectedExpiry;
        Assert.Equal(expected, expensive.TemporaryEnergyCost?.Expiry);
        var defend = combat.Cards.Single(c => c.CardId ==
            "proto.silent.defend");
        Assert.Null(defend.TemporaryEnergyCost);
    }

    private static RunState Start(string seed, CardInstance[] deck,
        string[] relics)
    {
        var state = new RunState("prototype-unbound", "prototype-0.1",
            seed, seed, 0, RunPhase.Combat,
            new PlayerState(9000, 9000, 0, deck,
                relics.Select(id => new RelicInstance(id,
                    PrototypeJson.EmptyObject())).ToArray(),
                new PotionInstance?[2]),
            PrototypeRng.CreateBundle(seed),
            PrototypeJson.EmptyObject(),
            new RunWorldState(PrototypeContent.RulesetId,
                PrototypeContent.CharacterId, 3, 1, 100,
                PrototypeRoomType.Elite, new MapState([]),
                new CombatState(1, 3, 0, [], [], [], [], [],
                    1, [], [], 1, Act: 3),
                null, null, null, null));
        var method = typeof(PrototypeGameEngine).GetMethod(
            "StartCombat", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return Assert.IsType<RunState>(method!.Invoke(null,
        [
            state, PrototypeRoomType.Elite,
            PrototypeContent.Encounter(
                PrototypeNativeGloryElites.SoulNexusEncounterId)
        ]));
    }
}
