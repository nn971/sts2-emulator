using Sts2Emulator.Core;

namespace Sts2Emulator.Core.Tests;

public sealed class V111VakuuEventTests
{
    private static readonly PrototypeGameEngine Engine = new();

    [Fact]
    public void OffersOneRelicFromEachSourcePoolUsingNativeEventShuffle()
    {
        const string seed = "vakuu-offer-shuffle";
        var stream = NativeRngV111Contexts.ForEvent(seed, "VAKUU");
        var bundle = new RngBundle([stream]);
        var expected = new List<string>();
        foreach (var sourcePool in NativeVakuuEventV111.RelicPools)
        {
            var pool = (string[])sourcePool.Clone();
            NativeRngV111.Shuffle(bundle, stream.StreamId, pool);
            expected.Add("take_" + pool[0]);
        }

        var state = NativeVakuuEventV111.CreateOpeningFixture(seed);
        Assert.Equal(expected, state.World!.Event!.OfferedChoiceIds);
        Assert.Equal(7, NativeRngV111.ToSnapshot(
            state.World.Event.NativeEventRng!).Counter);
        Assert.Equal(NativeRngV111.ToSnapshot(bundle.Streams[0]),
            NativeRngV111.ToSnapshot(state.World.Event.NativeEventRng!));
        Assert.Equal(state.World.Event.OfferedChoiceIds,
            Engine.GetLegalActions(state).Select(action =>
                action.ReadPayload<EventChoicePayload>().ChoiceId));
        PrototypeStateInvariants.Validate(state);
        Assert.Equal(CanonicalJson.Sha256(state), CanonicalJson.Sha256(
            RunSnapshot.Load(RunSnapshot.Save(state))));
    }

    [Fact]
    public void LordParasolBranchAcquiresRelicAndClearsOpeningEvent()
    {
        RunState? opening = null;
        var parasolChoice = "take_" + NativeGloryAncientsV111.LordsParasolId;
        for (var index = 0; index < 100 && opening is null; index++)
        {
            var candidate = NativeVakuuEventV111.CreateOpeningFixture(
                "vakuu-parasol-branch-" + index);
            if (candidate.World!.Event!.OfferedChoiceIds!.Contains(
                    parasolChoice, StringComparer.Ordinal))
                opening = candidate;
        }

        var state = Assert.IsType<RunState>(opening);
        var action = Engine.GetLegalActions(state).Single(candidate =>
            candidate.ReadPayload<EventChoicePayload>().ChoiceId
                == parasolChoice);
        state = Engine.Step(state, action).State;

        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == NativeGloryAncientsV111.LordsParasolId);
        Assert.Null(state.World!.Event);
        Assert.Equal(RunPhase.MapChoice, state.Phase);
        PrototypeStateInvariants.Validate(state);
    }

    [Fact]
    public void SereTalonBranchAppliesItsMaxHpCostBeforeAcquisition()
    {
        RunState? opening = null;
        var choiceId = "take_" + NativeGloryAncientsV111.SereTalonId;
        for (var index = 0; index < 100 && opening is null; index++)
        {
            var candidate = NativeVakuuEventV111.CreateOpeningFixture(
                "vakuu-sere-talon-branch-" + index);
            if (candidate.World!.Event!.OfferedChoiceIds!.Contains(
                    choiceId, StringComparer.Ordinal))
                opening = candidate;
        }

        var state = Assert.IsType<RunState>(opening);
        var originalMaxHp = state.Player.MaxHp;
        var action = Engine.GetLegalActions(state).Single(candidate =>
            candidate.ReadPayload<EventChoicePayload>().ChoiceId == choiceId);
        state = Engine.Step(state, action).State;

        Assert.Equal(originalMaxHp - 9, state.Player.MaxHp);
        Assert.Contains(state.Player.Relics, relic =>
            relic.RelicId == NativeGloryAncientsV111.SereTalonId);
        Assert.Null(state.World!.Event);
        PrototypeStateInvariants.Validate(state);
    }
}
