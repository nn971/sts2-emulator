using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111OvergrowthEventChoiceObservabilityTests
{
    [Fact]
    public void DenseVegetationShowsRolledGoldAndCurrentRestHealWithoutRngDraw()
    {
        var state = Event("proto.native.event.dense_vegetation",
            nativeGold: 83);
        state = state with
        {
            Player = state.Player with { Hp = 39, MaxHp = 70 }
        };
        var initialRng = CanonicalJson.Sha256(state.Rng);
        var initialRun = CanonicalJson.Sha256(state);
        var values = Values(state);
        Assert.Equal(83, Choice(values, "trudge").GoldGain);
        Assert.Equal(8, Choice(values, "trudge").HpLoss);
        Assert.Equal(21, Choice(values, "rest").Heal);
        Assert.Equal(initialRng, CanonicalJson.Sha256(state.Rng));
        Assert.Equal(initialRun, CanonicalJson.Sha256(state));
        Assert.Equal(values, Values(state.Fork()));

        var fullHp = state with
        {
            Player = state.Player with { Hp = 69 }
        };
        Assert.Equal(1, Choice(Values(fullHp), "rest").Heal);
    }

    [Fact]
    public void MorphicGroveCostTracksActualGoldAndLonerMaxHp()
    {
        var state = Event("proto.native.event.morphic_grove");
        state = state with
        {
            Player = state.Player with { Gold = 157 }
        };
        Assert.Equal(157, Choice(Values(state), "group").GoldCost);
        Assert.Equal(5, Choice(Values(state), "loner").MaxHpDelta);
        state = state with
        {
            Player = state.Player with { Gold = 240 }
        };
        Assert.Equal(240, Choice(Values(state), "group").GoldCost);
    }

    [Fact]
    public void OvergrowthEventsExposeKnownCardsAndFixedHealing()
    {
        var nest = Values(Event("proto.native.event.byrdonis_nest"));
        Assert.Equal(7, Choice(nest, "eat").MaxHpDelta);
        Assert.Equal("proto.native.event.byrdonis_egg",
            Choice(nest, "take").GuaranteedCardId);

        var sapphire = Values(Event("proto.native.event.sapphire_seed"));
        Assert.Equal(9, Choice(sapphire, "eat").Heal);

        var wells = Values(Event("proto.native.event.wellspring"));
        Assert.Equal("proto.native.event.guilty",
            Choice(wells, "bathe").GuaranteedCardId);

        var choir = Values(Event("proto.native.event.luminous_choir"));
        Assert.Equal("proto.native.event.spore_mind",
            Choice(choir, "reach").GuaranteedCardId);

        var carvings = Values(Event("proto.native.event.wood_carvings"));
        Assert.Equal("proto.native.event.peck",
            Choice(carvings, "bird").GuaranteedCardId);
        Assert.Equal("proto.native.event.toric_toughness",
            Choice(carvings, "torus").GuaranteedCardId);
    }

    [Fact]
    public void UnrestSiteRestAndMaxHpSacrificeReflectCurrentPlayerState()
    {
        var state = Event("proto.native.event.unrest_site");
        state = state with
        {
            Player = state.Player with { Hp = 25, MaxHp = 73 }
        };
        var values = Values(state);
        Assert.Equal(48, Choice(values, "rest").Heal);
        Assert.Equal("proto.native.event.poor_sleep",
            Choice(values, "rest").GuaranteedCardId);
        Assert.Equal(-8, Choice(values, "kill").MaxHpDelta);
    }

    [Fact]
    public void UnmodeledOrUnrevealedOutcomeDoesNotClaimGuaranteedItem()
    {
        var state = Event("proto.native.event.sapphire_seed");
        var observed = new PrototypeAiEnvironment().Observe(state)
            .Observation.Event!;
        Assert.DoesNotContain(observed.VisibleChoiceValues!, choice =>
            choice.ChoiceId == "plant" && choice.GuaranteedCardId is not null);
        Assert.DoesNotContain(
            System.Text.Json.JsonSerializer.Serialize(observed),
            "NativeEventGold");
        Assert.Null(new PrototypeAiEnvironment()
            .Observe(Event("proto.native.event.aroma_of_chaos"))
            .Observation.Event!.VisibleChoiceValues);
    }

    private static RunState Event(string eventId, int nativeGold = 0)
    {
        var state = PrototypeNativeOvergrowthRunFactory.Create(
            "obs-" + eventId + "-" + nativeGold);
        return state with
        {
            Phase = RunPhase.Event,
            World = state.World! with
            {
                ActiveRoom = PrototypeRoomType.Event,
                Event = new EventState(eventId,
                    NativeEventGold: nativeGold)
            }
        };
    }

    private static PrototypeAiEventChoiceValue[] Values(
        RunState state) =>
        new PrototypeAiEnvironment().Observe(state).Observation
            .Event!.VisibleChoiceValues!;

    private static PrototypeAiEventChoiceValue Choice(
        PrototypeAiEventChoiceValue[] values, string id) =>
        values.Single(choice => choice.ChoiceId == id);
}
