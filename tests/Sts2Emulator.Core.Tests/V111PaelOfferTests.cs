using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111PaelOfferTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void OpeningSamplesTheThreeConditionalPoolsInOrderAndPersistsTheEventCursor()
    {
        const string seed = "pael-native-offer-pools";
        var opening = PrototypeNativeLaterActFixture.Create(seed, 2);
        var originalHash = CanonicalJson.Sha256(opening);
        var pools = NativePaelRewardsV111.OfferPools(opening.Player);
        Assert.Equal(3, pools.Length);
        Assert.Equal(new[] { NativePaelRewardsV111.FleshId, NativePaelRewardsV111.HornId,
            NativePaelRewardsV111.TearsId }, pools[0]);
        var halfLength = (pools[1].Length - 1) / 2;
        var half = pools[1][..halfLength];
        Assert.Equal(half, pools[1][halfLength..^1]);
        Assert.Equal(NativePaelRewardsV111.GrowthId, pools[1][^1]);
        Assert.Contains(NativePaelRewardsV111.LegionId, pools[2]);

        var initial = NativeRngV111Contexts.ForEvent(seed, "PAEL");
        var expected = new RngBundle([initial]);
        var ids = pools.Select(pool => "take_" + NativeRngV111.NextItem(expected,
            initial.StreamId, (IReadOnlyList<string>)pool)).ToArray();
        var state = NativePaelRewardsV111.BeginPaelOpening(opening);

        Assert.Equal(originalHash, CanonicalJson.Sha256(opening));
        Assert.Equal(ids, state.World!.Event!.OfferedChoiceIds);
        Assert.Equal(ids, Engine.GetLegalActions(state)
            .Select(action => action.ReadPayload<EventChoicePayload>().ChoiceId));
        Assert.Equal(3, NativeRngV111.ToSnapshot(state.World.Event.NativeEventRng!).Counter);
        Assert.Equal(NativeRngV111.ToSnapshot(expected.Streams[0]),
            NativeRngV111.ToSnapshot(state.World.Event.NativeEventRng!));
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(state))));
    }

    [Fact]
    public void PoolTwoEligibilityAndPetProgressionMatchTheSourceRules()
    {
        var opening = PrototypeNativeLaterActFixture.Create("pael-conditional-pools", 2);
        var expandedDeck = opening.Player.Deck.Concat(Enumerable.Range(0, 5).Select(i =>
            new CardInstance(100_000 + i, "proto.silent.defend", 0, PrototypeJson.EmptyObject())))
            .ToArray();
        var expandedPools = NativePaelRewardsV111.OfferPools(opening.Player with { Deck = expandedDeck });
        Assert.Equal(new[] { NativePaelRewardsV111.WingId, NativePaelRewardsV111.ClawId,
            NativePaelRewardsV111.ToothId }, expandedPools[1][..3]);
        Assert.Equal(expandedPools[1][..3], expandedPools[1][3..6]);
        Assert.Equal(NativePaelRewardsV111.GrowthId, expandedPools[1][^1]);

        var egg = new CardInstance(200_000, "proto.native.event.byrdonis_egg", 0,
            PrototypeJson.EmptyObject());
        var eggPools = NativePaelRewardsV111.OfferPools(opening.Player with
            { Deck = opening.Player.Deck.Append(egg).ToArray() });
        Assert.DoesNotContain(NativePaelRewardsV111.LegionId, eggPools[2]);
        var byrdpipPools = NativePaelRewardsV111.OfferPools(opening.Player with
            { Relics = opening.Player.Relics.Append(new("proto.native.event.byrdpip",
                PrototypeJson.EmptyObject())).ToArray() });
        Assert.DoesNotContain(NativePaelRewardsV111.LegionId, byrdpipPools[2]);

        var meltedPetPlayer = opening.Player with
        {
            Relics = opening.Player.Relics.Append(new(NativePaelRewardsV111.LegionId,
                PrototypeJson.EmptyObject(), IsMelted: true)).ToArray()
        };
        Assert.True(NativePaelRewardsV111.HasEventPet(meltedPetPlayer));
        Assert.DoesNotContain(NativePaelRewardsV111.LegionId,
            NativePaelRewardsV111.OfferPools(meltedPetPlayer)[2]);
    }

    [Fact]
    public void OfferContextCannotBeReorderedOrEditedAndAncientBoundaryIsSingleUse()
    {
        var opening = PrototypeNativeLaterActFixture.Create("pael-invalid-offer-context", 2);
        var state = NativePaelRewardsV111.BeginPaelOpening(opening);
        Assert.Throws<InvalidOperationException>(() => NativePaelRewardsV111.BeginPaelOpening(state));
        Assert.Throws<InvalidOperationException>(() => NativeAncientRewardsV111.BeginTezcataraOpening(
            opening with { World = opening.World! with { EventHistory = [NativePaelRewardsV111.PaelId] } }));
        var offers = (string[])state.World!.Event!.OfferedChoiceIds!.Clone();
        (offers[0], offers[1]) = (offers[1], offers[0]);
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World with { Event = state.World.Event with { OfferedChoiceIds = offers } } }));
        var cursor = state.World.Event.NativeEventRng!;
        Assert.Throws<InvalidOperationException>(() => PrototypeStateInvariants.Validate(state with
        { World = state.World with { Event = state.World.Event with
            { NativeEventRng = cursor with { CallCount = cursor.CallCount - 1 } } } }));
    }
}
