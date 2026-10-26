using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111GloryGraveForgottenSoulTests
{
    private static readonly PrototypeGameEngine Engine = new();
    private const string Grave =
        PrototypeNativeGloryEvents.GraveOfTheForgottenId;
    private const string Decay = PrototypeNativeGloryEvents.DecayId;
    private const string Soul =
        PrototypeNativeGloryEvents.ForgottenSoulId;

    [Fact]
    public void GraveAndSoulsPowerAreGatedButSharedNativeContentIsReused()
    {
        var definition = PrototypeContent.Event(Grave);
        Assert.Equal((3, 3, 0),
            (definition.MinAct, definition.MaxAct, definition.Weight));
        Assert.Equal(new[] { "confront", "accept" },
            definition.Choices.Select(c => c.Id));
        Assert.Equal(PrototypeNativeHiveEvents.DecayId, Decay);
        Assert.Equal("Decay", PrototypeContent.Card(Decay).Name);
        Assert.Equal(2,
            PrototypeContent.Card(Decay).EndTurnDamageIfInHand);
        Assert.False(PrototypeContent.Card(Decay).EndTurnDamageUnblockable);
        Assert.True(PrototypeContent.Card(Decay).Unplayable);
        Assert.Contains(PrototypeContent.Relic(Soul).Triggers!,
            t => t.EventKind == PrototypeCombatEventKind.CardExhausted);
        Assert.DoesNotContain(Soul, PrototypeContent.RelicPool);
        Assert.DoesNotContain(Decay, PrototypeContent.RewardCardPool);
        Assert.Contains(PrototypeCardEnchantmentKind.SoulsPower,
            Enum.GetValues<PrototypeCardEnchantmentKind>());
    }

    [Fact]
    public void GraveConfrontIsLockedWhenNoExhaustCardCanBeEnchanted()
    {
        var state = Glory("grave-locked");
        state = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Where(card =>
                    !PrototypeContent.Card(card.CardId).ExhaustOnUse)
                    .ToArray()
            }
        };
        Assert.Equal(new[] { "accept" }, EventChoices(state));
        var before = CanonicalJson.Sha256(state);
        Assert.Throws<InvalidOperationException>(() =>
            Choice(state, "confront"));
        Assert.Equal(before, CanonicalJson.Sha256(state));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void ConfrontAddsDecayBeforePromptAndEnchantsExactlyOneExhaustCard()
    {
        var state = WithCard(Glory("grave-confront"),
            "proto.silent.blade_dance");
        var source = state.Player.Deck[^1];
        var sourceId = source.InstanceId;
        var originalDeck = state.Player.Deck;
        var previousNext = state.World!.NextCardInstanceId;
        Assert.Contains("confront", EventChoices(state));
        var originalHash = CanonicalJson.Sha256(state);

        var choosing = Choice(state, "confront");
        Assert.Equal(RunPhase.Event, choosing.Phase);
        Assert.Equal(originalDeck.Length + 1, choosing.Player.Deck.Length);
        Assert.Equal(Decay, choosing.Player.Deck[^1].CardId);
        Assert.Equal(previousNext + 1, choosing.World!.NextCardInstanceId);
        var pending = Assert.IsType<PrototypePendingEventDeckChoiceState>(
            choosing.World.Event!.PendingDeckChoice);
        Assert.Equal(PrototypePersistentDeckChoiceKind.Enchant,
            pending.Kind);
        Assert.Equal(PrototypeCardEnchantmentKind.SoulsPower,
            pending.EnchantmentKind);
        Assert.Contains(sourceId, pending.CandidateCardInstanceIds);
        Assert.DoesNotContain(choosing.Player.Deck[^1].InstanceId,
            pending.CandidateCardInstanceIds);
        PrototypeStateInvariants.Validate(choosing);

        var completed = Select(choosing, sourceId);
        completed = PrototypeEventPageTestHelperV111.ContinueIfFinished(completed);
        Assert.Equal(RunPhase.MapChoice, completed.Phase);
        var enchanted = completed.Player.Deck.Single(c =>
            c.InstanceId == sourceId);
        Assert.Equal(new PrototypeCardEnchantment(
            PrototypeCardEnchantmentKind.SoulsPower, 1),
            enchanted.Enchantment);
        Assert.Equal(1, completed.Player.Deck.Count(c => c.CardId == Decay));
        Assert.Equal(originalHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(completed),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Select(Choice(state.Fork(), "confront"), sourceId))));
        PrototypeStateInvariants.Validate(completed);
    }

    [Fact]
    public void SoulsPowerCannotEnchantAlreadyEnchantedOrNonExhaustingUpgrade()
    {
        var state = WithCard(Glory("grave-enchant-filter"),
            "proto.silent.blade_dance");
        state = WithCard(state, "proto.silent.mirage", level: 1);
        state = WithCard(state, "proto.silent.adrenaline",
            enchantment: new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.Spiral, 1));
        var eligible = state.Player.Deck[^3].InstanceId;
        var upgradedMirage = state.Player.Deck[^2].InstanceId;
        var enchantedAdrenaline = state.Player.Deck[^1].InstanceId;

        var choosing = Choice(state, "confront");
        var pending = choosing.World!.Event!.PendingDeckChoice!;
        Assert.Contains(eligible, pending.CandidateCardInstanceIds);
        Assert.DoesNotContain(upgradedMirage,
            pending.CandidateCardInstanceIds);
        Assert.DoesNotContain(enchantedAdrenaline,
            pending.CandidateCardInstanceIds);
        Assert.Throws<InvalidOperationException>(() =>
            Select(choosing, upgradedMirage));
        PrototypeStateInvariants.Validate(choosing);
    }

    [Fact]
    public void AcceptAcquiresExistingForgottenSoulAndCompletesEvent()
    {
        var state = Glory("grave-accept");
        var previous = CanonicalJson.Sha256(state);
        var result = Choice(state, "accept");
        result = PrototypeEventPageTestHelperV111.ContinueIfFinished(result);
        Assert.Equal(RunPhase.MapChoice, result.Phase);
        Assert.Single(result.Player.Relics, relic =>
            relic.RelicId == Soul);
        Assert.Null(result.World!.Event);
        Assert.Equal(previous, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(
                PrototypeEventPageTestHelperV111.ContinueIfFinished(
                    Choice(state.Fork(), "accept"))));
        PrototypeStateInvariants.Validate(result);
    }

    [Fact]
    public void ForgottenSoulDealsOneUnpoweredDamageToEnemyOnCardExhaust()
    {
        var without = CombatFixture(forgottenSoul: false);
        var with = CombatFixture(forgottenSoul: true);
        var card = PrototypeNativeHiveEvents.MetamorphosisId;
        without = InjectCombatCard(without, card);
        with = InjectCombatCard(with, card);
        var playedWithout = Play(without);
        var playedWith = Play(with);

        Assert.Single(playedWithout.World!.Combat!.ExhaustPile,
            id => id == without.World!.Combat!.NextCardInstanceId - 1);
        Assert.Single(playedWith.World!.Combat!.ExhaustPile,
            id => id == with.World!.Combat!.NextCardInstanceId - 1);
        var enemyWithout = Assert.Single(playedWithout.World.Combat.Enemies);
        var enemyWith = Assert.Single(playedWith.World.Combat.Enemies);
        Assert.Equal(enemyWithout.Hp, enemyWith.Hp);
        Assert.Equal(enemyWithout.Block - 1, enemyWith.Block);
        Assert.Equal(CanonicalJson.Sha256(playedWith),
            CanonicalJson.Sha256(Play(with.Fork())));
    }

    [Fact]
    public void SoulsPowerPreventsExhaustAndItsForgottenSoulTrigger()
    {
        const string cardId = "proto.native.event.spore_mind";
        var normal = InjectCombatCard(
            CombatFixture(forgottenSoul: true), cardId);
        var blessed = InjectCombatCard(
            CombatFixture(forgottenSoul: true), cardId,
            new PrototypeCardEnchantment(
                PrototypeCardEnchantmentKind.SoulsPower, 1));
        var normalId = normal.World!.Combat!.NextCardInstanceId - 1;
        var blessedId = blessed.World!.Combat!.NextCardInstanceId - 1;
        var exhausted = Play(normal);
        var retained = Play(blessed);
        Assert.Contains(normalId,
            exhausted.World!.Combat!.ExhaustPile);
        Assert.Contains(blessedId,
            retained.World!.Combat!.DiscardPile);
        Assert.DoesNotContain(blessedId,
            retained.World.Combat.ExhaustPile);
        var enemyExhausted = Assert.Single(exhausted.World.Combat.Enemies);
        var enemyRetained = Assert.Single(retained.World.Combat.Enemies);
        Assert.Equal(enemyRetained.Block - 1, enemyExhausted.Block);
        Assert.Equal(CanonicalJson.Sha256(retained),
            CanonicalJson.Sha256(Play(blessed.Fork())));
        // This isolated Mysterious Knight fight uses a two-node
        // synthetic map and deliberately bypasses native floor rules.
        // Combat/fork semantics are asserted above, not run-map validity.
    }

    [Fact]
    public void DecayDamageIsTwoAndBlockableDuringHandEnd()
    {
        var combat = CombatFixture(forgottenSoul: false);
        var original = combat.World!.Combat!;
        var id = original.NextCardInstanceId;
        var curse = new CombatCardInstance(id, null, Decay, 0, true,
            PrototypeJson.EmptyObject());
        var with = combat with
        {
            World = combat.World with
            {
                Combat = original with
                {
                    Cards = original.Cards.Append(curse).ToArray(),
                    NextCardInstanceId = id + 1,
                    Hand = original.Hand.Append(id).ToArray(),
                    PlayerBlock = 0
                }
            }
        };
        var without = with with
        {
            World = with.World! with
            {
                Combat = with.World.Combat! with
                {
                    Hand = with.World.Combat.Hand
                        .Where(cardId => cardId != id).ToArray(),
                    DiscardPile = with.World.Combat.DiscardPile
                        .Append(id).ToArray()
                }
            }
        };
        var endWith = End(with);
        var endWithout = End(without);
        Assert.Equal(endWithout.Player.Hp - 2, endWith.Player.Hp);
        Assert.Equal(CanonicalJson.Sha256(endWith),
            CanonicalJson.Sha256(End(with.Fork())));
    }

    private static RunState Glory(string seed) =>
        PrototypeNativeLaterActFixture.CreateGloryEvent(seed, Grave);

    private static RunState WithCard(
        RunState state, string cardId, int level = 0,
        PrototypeCardEnchantment? enchantment = null)
    {
        var world = state.World!;
        var card = new CardInstance(world.NextCardInstanceId, cardId,
            level, PrototypeJson.EmptyObject(), enchantment);
        return state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Append(card).ToArray()
            },
            World = world with
            {
                NextCardInstanceId = world.NextCardInstanceId + 1
            }
        };
    }

    private static string[] EventChoices(RunState state) =>
        Engine.GetLegalActions(state).Where(action =>
            action.Kind == "event_choice").Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId).ToArray();

    private static RunState Choice(RunState state, string choice) =>
        Engine.Step(state, GameAction.Create("event_choice",
            new EventChoicePayload(choice))).State;

    private static RunState Select(RunState state, long cardId) =>
        Engine.Step(state, GameAction.Create("choose_event_deck_card",
            new ChooseEventDeckCardPayload(cardId))).State;

    private static RunState CombatFixture(bool forgottenSoul)
    {
        var start = Engine.Step(PrototypeGameFactory.Create(
            "v111-grave-combat-test"),
            GameAction.Empty("start_run")).State;
        var relics = forgottenSoul
            ? start.Player.Relics.Append(new RelicInstance(
                Soul, PrototypeJson.EmptyObject())).ToArray()
            : start.Player.Relics;
        var state = start with
        {
            Phase = RunPhase.Event,
            Player = start.Player with { Relics = relics },
            World = start.World! with
            {
                Act = 2, Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    PrototypeNativeHiveEvents.LanternKeyEventId),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };
        state = Choice(state, "keep_key");
        state = Choice(state, "fight");
        Assert.Equal(RunPhase.Combat, state.Phase);
        return state;
    }

    private static RunState InjectCombatCard(
        RunState state, string cardId,
        PrototypeCardEnchantment? enchantment = null)
    {
        var combat = state.World!.Combat!;
        var id = combat.NextCardInstanceId;
        return state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    NextCardInstanceId = id + 1,
                    Cards = combat.Cards.Append(
                        new CombatCardInstance(id, null, cardId, 0, true,
                            PrototypeJson.EmptyObject(),
                            Enchantment: enchantment)).ToArray(),
                    Hand = combat.Hand.Append(id).ToArray()
                }
            }
        };
    }

    private static RunState Play(RunState state)
    {
        var id = state.World!.Combat!.NextCardInstanceId - 1;
        return Engine.Step(state, GameAction.Create("play_card",
            new PlayCardPayload(id, null))).State;
    }

    private static RunState End(RunState state) =>
        Engine.Step(state, GameAction.Empty("end_turn")).State;
}
