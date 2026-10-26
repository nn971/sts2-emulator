using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class PrototypePowerCardCompletionLifecycleTests
{
    // A Power is intentionally deleted from combat.Cards when played.
    // Post-play persistent-enchantment bookkeeping must not look it up after
    // that deletion; it must also still disable Vigorous on the run deck.
    [Theory]
    [InlineData("proto.silent.footwork", false)]
    [InlineData("proto.silent.footwork", true)]
    [InlineData("proto.silent.noxious_fumes", false)]
    [InlineData("proto.silent.noxious_fumes", true)]
    [InlineData("proto.silent.defend", true)]
    public void PlayingCardCompletesWithoutRequiringRemovedPowerIdentity(
        string cardId, bool vigorous)
    {
        const long combatCardId = 20;
        const long persistentCardId = 1020;
        var empty = PrototypeJson.EmptyObject();
        var enchantment = vigorous
            ? new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Vigorous)
            : null;
        var card = new CombatCardInstance(
            combatCardId, persistentCardId, cardId, 0, false, empty,
            Enchantment: enchantment);
        var persistentCard = new CardInstance(
            persistentCardId, cardId, 0, empty, enchantment);

        var player = new PlayerState(
            70, 70, 0, [persistentCard], [],
            new PotionInstance?[PrototypeContent.Rules.PotionSlots]);
        var combat = new CombatState(
            Turn: 1,
            Energy: 3,
            PlayerBlock: 0,
            Hand: [combatCardId],
            DrawPile: [],
            DiscardPile: [],
            ExhaustPile: [],
            Enemies: [
                new EnemyCombatState(
                    1, "proto.enemy.crawler", 999, 0, 0,
                    new Dictionary<string, int>(StringComparer.Ordinal))
            ],
            NextCardInstanceId: combatCardId + 1,
            Cards: [card],
            PlayerPowers: [],
            NextPowerApplicationOrder: 1);
        var state = new RunState(
            "prototype-unbound", "prototype-0.1",
            "h6-power-card-completion", "h6-power-card-completion", 0,
            RunPhase.Combat, player,
            PrototypeRng.CreateBundle("h6-power-card-completion"),
            empty,
            new RunWorldState(
                PrototypeContent.RulesetId, PrototypeContent.CharacterId,
                1, 1, persistentCardId + 1,
                PrototypeRoomType.Combat, new MapState([]),
                combat, null, null, null, null));

        var engine = new PrototypeGameEngine();
        var play = Assert.Single(engine.GetLegalActions(state)
            .Where(action => action.Kind == "play_card"));
        Assert.Equal(combatCardId,
            play.ReadPayload<PlayCardPayload>().CardInstanceId);
        state = engine.Step(state, play).State;

        PrototypeStateInvariants.Validate(state);
        var after = state.World!.Combat!;
        var isPower = PrototypeContent.Card(cardId).Type
            == PrototypeCardType.Power;
        if (isPower)
        {
            Assert.DoesNotContain(after.Cards,
                candidate => candidate.InstanceId == combatCardId);
            Assert.DoesNotContain(combatCardId,
                after.Hand.Concat(after.DrawPile)
                    .Concat(after.DiscardPile).Concat(after.ExhaustPile));
        }
        else
        {
            Assert.Contains(after.Cards,
                candidate => candidate.InstanceId == combatCardId);
            Assert.Contains(combatCardId, after.DiscardPile);
        }

        var persisted = Assert.Single(state.Player.Deck);
        Assert.Equal(persistentCardId, persisted.InstanceId);
        if (vigorous)
        {
            Assert.NotNull(persisted.Enchantment);
            Assert.Equal(PrototypeCardEnchantmentKind.Vigorous,
                persisted.Enchantment!.Kind);
            Assert.True(persisted.Enchantment.Disabled);
        }
        else
        {
            Assert.Null(persisted.Enchantment);
        }
    }
}
