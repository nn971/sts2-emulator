using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111OrobasEventTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void EachSupportedRelicOfferResolvesThroughTheExistingAcquisitionFlow()
    {
        var openings = Enumerable.Range(0, 96).Select(index =>
            NativeOrobasEventV111.CreateOpeningFixture("orobas-branches-" + index))
            .ToArray();
        foreach (var choice in NativeOrobasEventV111.Definition.Choices
            .Where(choice => choice.Id != NativeOrobasEventV111.LockedChoiceId))
        {
            var initial = openings.First(state =>
                state.World!.Event!.OfferedChoiceIds!.Contains(choice.Id));
            var beforeHash = CanonicalJson.Sha256(initial);
            var action = GameAction.Create("event_choice",
                new EventChoicePayload(choice.Id));
            var selected = Engine.Step(initial, action).State;
            var relicId = Assert.Single(choice.Effects).RelicId!;
            Assert.Single(selected.Player.Relics, relic => relic.RelicId == relicId);
            Assert.Equal(CanonicalJson.Sha256(selected), CanonicalJson.Sha256(
                Engine.Step(initial.Fork(), action).State));
            var completed = CompleteOpening(selected);
            var resumed = CompleteOpening(RunSnapshot.Load(RunSnapshot.Save(selected)));
            Assert.Equal(CanonicalJson.Sha256(completed), CanonicalJson.Sha256(resumed));
            Assert.Equal(RunPhase.MapChoice, completed.Phase);
            Assert.Null(completed.World!.Event);
            Assert.Equal(0, completed.World.Floor);
            Assert.Empty(completed.World.CompletedRooms);
            Assert.Equal(beforeHash, CanonicalJson.Sha256(initial));
            Assert.Throws<InvalidOperationException>(() =>
                NativeOrobasEventV111.BeginOpening(completed));

            if (relicId == PrototypeNativeOrobasRelics.GlassEyeId)
                Assert.Equal(initial.Player.Deck.Length + 5, completed.Player.Deck.Length);
            if (relicId == PrototypeNativeOrobasRelics.ElectricShrympId)
                Assert.Single(completed.Player.Deck, card =>
                    card.Enchantment?.Kind == PrototypeCardEnchantmentKind.Imbued);
            if (relicId == PrototypeNativeOrobasRelics.SandCastleId)
                Assert.Equal(6, completed.Player.Deck.Count(card => card.UpgradeLevel == 1));
            if (relicId == PrototypeNativeOrobasRelics.ArchaicToothId)
                Assert.Contains(completed.Player.Deck, card =>
                    card.CardId == PrototypeNativeOrobasRelics.SuppressId);
            if (relicId == PrototypeNativeOrobasRelics.TouchOfOrobasId)
                Assert.Contains(completed.Player.Relics, relic =>
                    relic.RelicId == PrototypeNativeOrobasRelics.RingOfTheDrakeId);
            if (relicId == PrototypeNativeOrobasRelics.AlchemicalCofferId)
            {
                Assert.Equal(initial.Player.PotionSlots.Length + 4,
                    completed.Player.PotionSlots.Length);
                Assert.Equal(4, completed.Player.PotionSlots.Count(potion => potion is not null));
            }
        }
    }

    [Fact]
    public void StarterReplacementPoolLocksWhenBothDependenciesAreAbsent()
    {
        var map = PrototypeNativeLaterActFixture.Create("orobas-locked", 2);
        map = map with
        {
            Player = map.Player with
            {
                Relics = [],
                Deck = map.Player.Deck.Where(card =>
                    card.CardId != PrototypeNativeOrobasRelics.NeutralizeId).ToArray()
            }
        };
        var initialHash = CanonicalJson.Sha256(map);
        var state = NativeOrobasEventV111.BeginOpening(map);
        Assert.Equal(NativeOrobasEventV111.LockedChoiceId,
            state.World!.Event!.OfferedChoiceIds![2]);
        Assert.Equal(2, Engine.GetLegalActions(state).Count);
        Assert.Throws<InvalidOperationException>(() => Engine.Step(state,
            GameAction.Create("event_choice",
                new EventChoicePayload(NativeOrobasEventV111.LockedChoiceId))));
        Assert.Equal(initialHash, CanonicalJson.Sha256(map));
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void OffersAreStableAndRejectMissingContextAndUnofferedChoices()
    {
        var state = NativeOrobasEventV111.CreateOpeningFixture("orobas-stable");
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(3, state.World!.Event!.OfferedChoiceIds!.Length);
        var absent = NativeOrobasEventV111.Definition.Choices.First(choice =>
            !state.World.Event.OfferedChoiceIds.Contains(choice.Id));
        var before = CanonicalJson.Sha256(state);
        Assert.Throws<InvalidOperationException>(() => Engine.Step(state,
            GameAction.Create("event_choice", new EventChoicePayload(absent.Id))));
        Assert.Equal(before, CanonicalJson.Sha256(state));
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(
            state with
            {
                World = state.World with
                {
                    Event = state.World.Event with { NativeEventRng = null }
                }
            }));
    }

    private static RunState CompleteOpening(RunState state)
    {
        for (var decision = 0; decision < 32
            && state.Phase is RunPhase.Event or RunPhase.Reward; decision++)
        {
            PrototypeStateInvariants.Validate(state);
            var action = Engine.GetLegalActions(state)
                .First(action => action.Kind != "use_potion");
            state = Engine.Step(state, action).State;
        }
        PrototypeStateInvariants.Validate(state);
        return state;
    }
}
