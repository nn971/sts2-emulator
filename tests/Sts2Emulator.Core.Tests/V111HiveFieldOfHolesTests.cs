using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111HiveFieldOfHolesTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void SourceEligibilityAndGatedEventRegistration()
    {
        var definition = PrototypeContent.Event(
            PrototypeNativeHiveEvents.FieldOfManSizedHolesId);
        Assert.Equal((2, 2, 0),
            (definition.MinAct, definition.MaxAct, definition.Weight));
        Assert.Equal(new[] { "resist", "enter_your_hole" },
            definition.Choices.Select(x => x.Id));
        var start = Fixture();
        Assert.True(PrototypeNativeHiveEvents.IsEligible(
            definition, start.Player));
        Assert.False(PrototypeNativeHiveEvents.IsEligible(
            definition, start.Player with { Deck = [] }));
        Assert.True(PrototypeNativeHiveEvents.IsEligible(
            definition, start.Player with
            {
                Deck = [start.Player.Deck[0]]
            }));
        Assert.True(PrototypeContent.Card(
            PrototypeNativeHiveEvents.NormalityId).Unplayable);
        Assert.Equal(0, PrototypeContent.Card(
            PrototypeNativeHiveEvents.NormalityId).MaxUpgradeLevel);
    }

    [Fact]
    public void ResistRemovesTwoDeckCardsThenAddsNormality()
    {
        var state = Fixture();
        var initial = CanonicalJson.Sha256(state);
        var first = state.Player.Deck[0].InstanceId;
        var second = state.Player.Deck[1].InstanceId;
        var newId = state.World!.NextCardInstanceId;
        var startCount = state.Player.Deck.Length;
        state = Select(state, "resist");
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(startCount, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.Deck, c =>
            c.CardId == PrototypeNativeHiveEvents.NormalityId);
        Assert.Equal(2,
            state.World!.Event!.PendingDeckChoice!.RemainingSelections);

        state = ChooseCard(state, first);
        Assert.Equal(RunPhase.Event, state.Phase);
        Assert.Equal(startCount - 1, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.Deck, c =>
            c.CardId == PrototypeNativeHiveEvents.NormalityId);

        state = ChooseCard(state, second);
        state = PrototypeEventPageTestHelperV111.ContinueIfFinished(state);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(startCount - 1, state.Player.Deck.Length);
        Assert.DoesNotContain(state.Player.Deck, c =>
            c.InstanceId == first || c.InstanceId == second);
        var curse = Assert.Single(state.Player.Deck, c =>
            c.CardId == PrototypeNativeHiveEvents.NormalityId);
        Assert.Equal(newId, curse.InstanceId);
        Assert.Equal(newId + 1, state.World!.NextCardInstanceId);
        Assert.Equal(initial, CanonicalJson.Sha256(Fixture()));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void EnterYourHoleEnchantsOneSelectedEligibleCard()
    {
        var state = Fixture();
        var selected = state.Player.Deck[0];
        state = Select(state, "enter_your_hole");
        Assert.Equal(RunPhase.Event, state.Phase);
        var actions = Engine.GetLegalActions(state);
        Assert.Contains(actions, action =>
            action.Kind == "choose_event_deck_card" &&
            action.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == selected.InstanceId);

        state = ChooseCard(state, selected.InstanceId);
        state = PrototypeEventPageTestHelperV111.ContinueIfFinished(state);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        Assert.Equal(PrototypeCardEnchantmentKind.PerfectFit,
            state.Player.Deck.Single(c =>
                c.InstanceId == selected.InstanceId).Enchantment!.Kind);
        Assert.All(state.Player.Deck.Where(c =>
            c.InstanceId != selected.InstanceId),
            c => Assert.Null(c.Enchantment));
        Assert.Equal(CanonicalJson.Sha256(state),
            CanonicalJson.Sha256(state.Fork()));
    }

    [Fact]
    public void PerfectFitDoesNotChangeInitialShuffle()
    {
        var initial = StartedRun("holes-initial-shuffle");
        var modified = initial.Fork() with
        {
            Player = initial.Player with
            {
                Deck = initial.Player.Deck.Select((c, i) =>
                    i == 0 ? c with
                    {
                        Enchantment = new PrototypeCardEnchantment(
                            PrototypeCardEnchantmentKind.PerfectFit)
                    } : c).ToArray()
            }
        };
        var node = Engine.GetLegalActions(initial).First();
        var plain = Engine.Step(initial, node).State;
        var enchanted = Engine.Step(modified, node).State;
        Assert.Equal(RunPhase.Combat, plain.Phase);
        Assert.Equal(RunPhase.Combat, enchanted.Phase);
        Assert.Equal(plain.World!.Combat!.Hand,
            enchanted.World!.Combat!.Hand);
        Assert.Equal(plain.World.Combat.DrawPile,
            enchanted.World.Combat.DrawPile);
        Assert.Equal(PrototypeCardEnchantmentKind.PerfectFit,
            enchanted.World.Combat.Cards.Single(c =>
                c.PersistentCardInstanceId
                == modified.Player.Deck[0].InstanceId).Enchantment!.Kind);
    }

    [Fact]
    public void PerfectFitIsFirstDrawnAfterNonInitialShuffle()
    {
        var state = FirstCombat("holes-perfect-fit-reshuffle");
        var combat = state.World!.Combat!;
        var topCard = combat.Cards[0];
        var ids = combat.Cards.Select(c => c.InstanceId).ToArray();
        combat = combat with
        {
            Hand = [],
            DrawPile = [],
            DiscardPile = ids,
            ExhaustPile = [],
            Cards = combat.Cards.Select(c =>
                c.InstanceId == topCard.InstanceId ? c with
                {
                    Enchantment = new PrototypeCardEnchantment(
                        PrototypeCardEnchantmentKind.PerfectFit)
                } : c).ToArray()
        };
        state = state with
        {
            Player = state.Player with
            {
                Deck = state.Player.Deck.Select(c =>
                    c.InstanceId == topCard.PersistentCardInstanceId
                    ? c with
                    {
                        Enchantment = new PrototypeCardEnchantment(
                            PrototypeCardEnchantmentKind.PerfectFit)
                    } : c).ToArray()
            },
            World = state.World with { Combat = combat }
        };
        PrototypeStateInvariants.Validate(state);
        var initialHash = CanonicalJson.Sha256(state);
        var choice = Engine.GetLegalActions(state)
            .Single(a => a.Kind == "end_turn");
        var result = Engine.Step(state, choice).State;
        Assert.Equal(RunPhase.Combat, result.Phase);
        Assert.NotEmpty(result.World!.Combat!.Hand);
        Assert.Equal(topCard.InstanceId, result.World.Combat.Hand[0]);
        Assert.Equal(initialHash, CanonicalJson.Sha256(state));
        Assert.Equal(CanonicalJson.Sha256(result),
            CanonicalJson.Sha256(Engine.Step(state.Fork(), choice).State));
    }

    [Fact]
    public void NormalityOnlyLimitsCardsPlayedWhileHeld()
    {
        var state = FirstCombat("holes-normality-cap");
        var combat = state.World!.Combat!;
        var playable = combat.Cards.First(c =>
            c.CardId == "proto.silent.strike");
        var curseId = combat.NextCardInstanceId;
        var normality = new CombatCardInstance(
            curseId, null, PrototypeNativeHiveEvents.NormalityId,
            0, true, PrototypeJson.EmptyObject());
        combat = combat with
        {
            Hand = [playable.InstanceId, curseId],
            DrawPile = combat.Cards
                .Where(c => c.InstanceId != playable.InstanceId)
                .Select(c => c.InstanceId).ToArray(),
            DiscardPile = [],
            Cards = combat.Cards.Append(normality).ToArray(),
            NextCardInstanceId = curseId + 1,
            Energy = 3,
            Counters = combat.CounterState with
            {
                CardsPlayedThisTurn = 3
            }
        };
        state = state with { World = state.World with { Combat = combat } };
        PrototypeStateInvariants.Validate(state);
        var action = GameAction.Create("play_card",
            new PlayCardPayload(playable.InstanceId,
                combat.Enemies[0].InstanceId));
        Assert.DoesNotContain(Engine.GetLegalActions(state),
            a => a.Kind == "play_card");
        Assert.Throws<InvalidOperationException>(() =>
            Engine.Step(state, action));

        var withoutCurse = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Hand = [playable.InstanceId],
                    DrawPile = combat.DrawPile.Append(curseId).ToArray()
                }
            }
        };
        Assert.Contains(Engine.GetLegalActions(withoutCurse),
            a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == playable.InstanceId);
        var twoPlays = state with
        {
            World = state.World with
            {
                Combat = combat with
                {
                    Counters = combat.CounterState with
                    {
                        CardsPlayedThisTurn = 2
                    }
                }
            }
        };
        Assert.Contains(Engine.GetLegalActions(twoPlays),
            a => a.Kind == "play_card"
                && a.ReadPayload<PlayCardPayload>()
                    .CardInstanceId == playable.InstanceId);
    }

    private static RunState Fixture()
    {
        var state = StartedRun("hive-holes-event");
        return state with
        {
            Phase = RunPhase.Event,
            World = state.World! with
            {
                Act = 2,
                Floor = 1,
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(
                    PrototypeNativeHiveEvents.FieldOfManSizedHolesId),
                Map = new MapState(
                [
                    new("event", 2, 1, PrototypeRoomType.Event, ["next"]),
                    new("next", 2, 2, PrototypeRoomType.Combat, [])
                ], "event", ["event"])
            }
        };
    }

    private static RunState StartedRun(string seed) =>
        Engine.Step(PrototypeGameFactory.Create(seed),
            GameAction.Empty("start_run")).State;

    private static RunState FirstCombat(string seed)
    {
        var run = StartedRun(seed);
        var action = Engine.GetLegalActions(run).First();
        return Engine.Step(run, action).State;
    }

    private static RunState Select(RunState state, string choiceId)
    {
        var action = Engine.GetLegalActions(state).Single(a =>
            a.Kind == "event_choice"
            && a.ReadPayload<EventChoicePayload>().ChoiceId == choiceId);
        return Engine.Step(state, action).State;
    }

    private static RunState ChooseCard(RunState state, long id)
    {
        var action = Engine.GetLegalActions(state).Single(a =>
            a.Kind == "choose_event_deck_card"
            && a.ReadPayload<ChooseEventDeckCardPayload>()
                .CardInstanceId == id);
        return Engine.Step(state, action).State;
    }
}
