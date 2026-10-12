using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111TezcataraOfferTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OffersFollowCompleteOrderedSourcePoolsAndOwnThreeNativeDraws(bool hasStrike)
    {
        const string seed = "tezcatara-native-offer-pools";
        var opening = PrototypeNativeLaterActFixture.Create(seed, 2);
        if (!hasStrike)
            opening = opening with { Player = opening.Player with { Deck = opening.Player.Deck
                .Where(card => card.CardId != "proto.silent.strike").ToArray() } };
        var hash = CanonicalJson.Sha256(opening);
        var expectedStream = NativeRngV111Contexts.ForEvent(seed, "TEZCATARA");
        var expected = new RngBundle([expectedStream]);
        string[] first = hasStrike
            ? [NativeAncientRewardsV111.VeryHotCocoaId, NativeAncientRewardsV111.YummyCookieId, NativeAncientRewardsV111.NutritiousSoupId]
            : [NativeAncientRewardsV111.VeryHotCocoaId, NativeAncientRewardsV111.YummyCookieId];
        string[] second = [NativeAncientRewardsV111.BiiigHugId, NativeAncientRewardsV111.StorybookId, NativeAncientRewardsV111.ToastyMittensId];
        string[] third = [NativeAncientRewardsV111.GoldenCompassId, NativeAncientRewardsV111.PumpkinCandleId,
            NativeAncientRewardsV111.ToyBoxId, NativeAncientRewardsV111.SealOfGoldId];
        var ids = new[] { first, second, third }.Select(pool =>
            "take_" + NativeRngV111.NextItem(expected, expectedStream.StreamId, pool)).ToArray();
        var state = NativeAncientRewardsV111.BeginTezcataraOpening(opening);
        Assert.Equal(hash, CanonicalJson.Sha256(opening));
        Assert.Equal(ids, state.World!.Event!.OfferedChoiceIds);
        Assert.Equal(ids, Engine.GetLegalActions(state).Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId));
        Assert.Equal(NativeRngV111.ToSnapshot(expected.Streams[0]),
            NativeRngV111.ToSnapshot(state.World.Event.NativeEventRng!));
        Assert.Equal(3, NativeRngV111.ToSnapshot(state.World.Event.NativeEventRng!).Counter);
        Assert.Equal(CanonicalJson.Sha256(opening.Rng), CanonicalJson.Sha256(state.Rng));
        Assert.Equal(CanonicalJson.Sha256(opening.NativeRunRng), CanonicalJson.Sha256(state.NativeRunRng));
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(RunSnapshot.Load(RunSnapshot.Save(state))));
    }

    [Fact]
    public void AllTenRewardsAreReachableWithoutRemovingSoupForAlreadyEnchantedStrikes()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var withoutStrike = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < 96; i++)
        {
            var opening = PrototypeNativeLaterActFixture.Create("tezcatara-complete-offers-" + i, 2);
            opening = opening with { Player = opening.Player with { Deck = opening.Player.Deck.Select(card =>
                card.CardId == "proto.silent.strike"
                    ? card with { Enchantment = new(PrototypeCardEnchantmentKind.Spiral) } : card).ToArray() } };
            var offered = NativeAncientRewardsV111.BeginTezcataraOpening(opening);
            seen.UnionWith(offered.World!.Event!.OfferedChoiceIds!);
            var noStrikes = opening with { Player = opening.Player with { Deck = opening.Player.Deck
                .Where(card => card.CardId != "proto.silent.strike").ToArray() } };
            withoutStrike.UnionWith(NativeAncientRewardsV111.BeginTezcataraOpening(noStrikes).World!.Event!.OfferedChoiceIds!);
        }
        Assert.Equal(NativeAncientRewardsV111.Relics.Select(relic => "take_" + relic.Id).Order(), seen.Order());
        Assert.DoesNotContain("take_" + NativeAncientRewardsV111.NutritiousSoupId, withoutStrike);
    }

    [Fact]
    public void SnapshotAndForkOwnTheContextAndSelectionCannotAcquireAnUnseenReward()
    {
        var state = NativeAncientRewardsV111.CreateTezcataraOpeningFixture("tezcatara-context-ownership");
        var hash = CanonicalJson.Sha256(state);
        var fork = state.Fork();
        fork.World!.Event!.NativeEventRng!.StateBytes[0] ^= 1;
        fork.World.Event.OfferedChoiceIds![0] = "changed";
        Assert.Equal(hash, CanonicalJson.Sha256(state));
        foreach (var action in Engine.GetLegalActions(state))
        {
            var selected = Engine.Step(state, action).State;
            Assert.Equal(CanonicalJson.Sha256(selected), CanonicalJson.Sha256(
                Engine.Step(RunSnapshot.Load(RunSnapshot.Save(state)), action).State));
            PrototypeStateInvariants.Validate(selected);
        }
        var unoffered = NativeAncientRewardsV111.Relics.Select(relic => "take_" + relic.Id)
            .First(id => !state.World!.Event!.OfferedChoiceIds!.Contains(id));
        Assert.Throws<InvalidOperationException>(() => Engine.Step(state,
            GameAction.Create("event_choice", new EventChoicePayload(unoffered))));
        Assert.Equal(hash, CanonicalJson.Sha256(state));
    }

    [Fact]
    public void LateOrRepeatedOpeningAndCorruptedContextAreRejected()
    {
        var opening = PrototypeNativeLaterActFixture.Create("tezcatara-invalid-boundary", 2);
        Assert.Throws<InvalidOperationException>(() => NativeAncientRewardsV111.BeginTezcataraOpening(
            opening with { World = opening.World! with { Floor = 1 } }));
        var state = NativeAncientRewardsV111.BeginTezcataraOpening(opening);
        Assert.Throws<InvalidOperationException>(() => NativeAncientRewardsV111.BeginTezcataraOpening(state));
        var stream = state.World!.Event!.NativeEventRng!;
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World with { Event = state.World.Event with { NativeEventRng = stream with { CallCount = 2 } } } }));
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World with { Event = state.World.Event with { OfferedChoiceIds = ["one"] } } }));
        var swapped = (string[])state.World.Event.OfferedChoiceIds!.Clone();
        (swapped[0], swapped[1]) = (swapped[1], swapped[0]);
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World with { Event = state.World.Event with { OfferedChoiceIds = swapped } } }));
        var wrongSamePoolOffer = (string[])state.World.Event.OfferedChoiceIds.Clone();
        wrongSamePoolOffer[0] = "take_" + NativeAncientRewardsV111.Relics
            .Select(relic => relic.Id)
            .First(id => id != wrongSamePoolOffer[0][5..]
                && id is NativeAncientRewardsV111.VeryHotCocoaId
                    or NativeAncientRewardsV111.YummyCookieId
                    or NativeAncientRewardsV111.NutritiousSoupId);
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World with { Event = state.World.Event with { OfferedChoiceIds = wrongSamePoolOffer } } }));
        var corrupted = state.Fork();
        corrupted.World!.Event!.NativeEventRng!.StateBytes[0] ^= 1;
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(corrupted));
    }
}
