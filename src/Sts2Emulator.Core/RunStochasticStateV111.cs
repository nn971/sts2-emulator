namespace Sts2Emulator.Core;

/// <summary>
/// Atomic run-level ownership of provider-private stochastic state. The
/// compatibility properties on <see cref="RunState"/> project this envelope
/// under their existing serialized names until the snapshot wire schema is
/// deliberately migrated.
/// </summary>
public sealed record RunStochasticStateV111(
    RngBundle PrototypeRng,
    RngBundle? NativeRunRng = null,
    ReferenceNatureStateV111? ReferenceNature = null,
    ControlledEventChoiceTraceV111[]? ControlledEventChoiceHistory = null,
    PrototypeControlledEventRewardChoiceContinuationV111?
        ControlledEventRewardChoice = null,
    ControlledRunActionTraceV111[]? ControlledRunActionHistory = null,
    PrototypeControlledRunActionContinuationV111?
        ControlledRunActionChoice = null,
    SemanticOtherChanceStateV111? SemanticOtherChance = null,
    SemanticRewardAuditV111? SemanticRewardAudit = null)
{
    public RunStochasticStateV111 Fork() => this with
    {
        PrototypeRng = PrototypeRng.Fork(),
        NativeRunRng = NativeRunRng?.Fork(),
        ReferenceNature = ReferenceNature?.Fork(),
        ControlledEventChoiceHistory = ControlledEventChoiceHistory?
            .Select(trace => trace.Fork()).ToArray(),
        ControlledEventRewardChoice = ControlledEventRewardChoice?.Fork(),
        ControlledRunActionHistory = ControlledRunActionHistory?
            .Select(trace => trace.Fork()).ToArray(),
        ControlledRunActionChoice = ControlledRunActionChoice?.Fork(),
        SemanticOtherChance = SemanticOtherChance?.Fork()
    };

    public void Validate(RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        global::Sts2Emulator.Core.PrototypeRng.ValidateBundle(
            PrototypeRng,
            allowEmpty: owner.Phase == RunPhase.RunStart);
        if (ControlledEventChoiceHistory is { } controlledEventHistory)
        {
            if (controlledEventHistory.Any(trace => trace is null))
                throw new InvalidOperationException(
                    "Controlled event-choice history contains a null entry.");
            foreach (var trace in controlledEventHistory)
                trace.Validate();
        }
        if (ControlledRunActionHistory is { } controlledRunActionHistory)
        {
            if (controlledRunActionHistory.Any(trace => trace is null))
                throw new InvalidOperationException(
                    "Controlled run-action history contains a null entry.");
            foreach (var trace in controlledRunActionHistory)
                trace.Validate();
        }
        NativeRunRngV111.Validate(owner);
        ReferenceNature?.Validate();
        if (SemanticOtherChance is not null)
            SemanticOtherChanceEngineV111.Validate(SemanticOtherChance);
        if (SemanticRewardAudit is { } reward &&
            (owner.Phase != RunPhase.Reward ||
             reward.ProviderId != SemanticRewardAuditV111.PrototypeProviderId ||
             reward.ProbabilityStatus != "UNVERIFIED" ||
             reward.AfterCalls < reward.BeforeCalls ||
             owner.Rng.Streams.Single(x =>
                 x.StreamId == "reward").CallCount != reward.AfterCalls))
            throw new InvalidDataException(
                "Invalid source-timed reward RNG audit.");
        if (owner.World is { } world)
            world.Stochastic.Validate(world, owner);
    }

    internal void ValidateControlledRunActionChoice(RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (ControlledRunActionChoice is not { } controlledRunActionChoice)
            return;
        controlledRunActionChoice.Validate();
        if (owner.DecisionIndex <
                controlledRunActionChoice.PartialState.DecisionIndex
            || CanonicalJson.Sha256(owner with
                { ControlledRunActionChoice = null }) !=
            CanonicalJson.Sha256(
                controlledRunActionChoice.PartialState))
            throw new InvalidOperationException(
                "Suspended controlled run action conflicts with committed state.");
    }

    internal void ValidateControlledEventRewardChoice(RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (ControlledEventRewardChoice is not { } controlledEventRewardChoice)
            return;
        controlledEventRewardChoice.Validate();
        var world = owner.World;
        var rewardEvent = world?.Event;
        var reward = world?.Reward;
        if (world?.Event?.ControlledChoice is not null
            || owner.Phase != RunPhase.Reward
            || rewardEvent is null
            || rewardEvent.EventId != controlledEventRewardChoice.EventId
            || rewardEvent.ChosenChoiceId !=
                controlledEventRewardChoice.ChoiceId
            || reward?.SourceRoom !=
                "Event:" + controlledEventRewardChoice.EventId
            || reward?.AddRandomCurseAfterReward != true
            || CanonicalJson.Sha256(owner with
                { ControlledEventRewardChoice = null }) !=
            CanonicalJson.Sha256(
                controlledEventRewardChoice.PartialState))
            throw new InvalidOperationException(
                "Suspended controlled event reward conflicts with committed reward state.");
    }

    internal void ValidateNativeEventContexts(RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeUnderdocksTrashHeap.EventId,
            PrototypeNativeUnderdocksTrashHeap.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeTabletOfTruth.EventId,
            PrototypeNativeTabletOfTruth.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeUnderdocksDoors.EventId,
            PrototypeNativeUnderdocksDoors.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeFakeMerchant.EventId,
            PrototypeNativeFakeMerchant.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeGloryEvents.TinkerTimeId,
            PrototypeNativeGloryEvents.TinkerTimeNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeStoneOfAllTime.EventId,
            PrototypeNativeStoneOfAllTime.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeGloryEvents.BattlewornDummyId,
            PrototypeNativeGloryEvents.BattlewornDummyNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeGloryEvents.HungryForMushroomsId,
            PrototypeNativeGloryEvents.HungryForMushroomsNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeGloryEvents.ReflectionsId,
            PrototypeNativeGloryEvents.ReflectionsNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeGloryEvents.TrialId,
            PrototypeNativeGloryEvents.TrialNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeFutureOfPotions.EventId,
            PrototypeNativeFutureOfPotions.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeHiveEvents.InfestedAutomatonId,
            PrototypeNativeHiveEvents.InfestedAutomatonNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeEndlessConveyor.EventId,
            PrototypeNativeEndlessConveyor.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativePunchOff.EventId,
            PrototypeNativePunchOff.NativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeSharedEvents.ThisOrThatId,
            PrototypeNativeSharedEvents.ThisOrThatNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeHiveEvents.LostWispId,
            PrototypeNativeHiveEvents.LostWispNativeRngEventId);
        NativeEventContextRngV111.Validate(
            owner,
            PrototypeNativeDenseVegetation.EventId,
            "DENSE_VEGETATION");
        NativeEventContextRngV111.Validate(
            owner,
            "proto.native.event.whispering_hollow",
            "WHISPERING_HOLLOW");
        NativeEventContextRngV111.Validate(
            owner,
            "proto.native.event.sunken_statue",
            "SUNKEN_STATUE");
        NativeEventContextRngV111.Validate(
            owner,
            "proto.native.event.luminous_choir",
            "LUMINOUS_CHOIR");
        NativeEventContextRngV111.Validate(
            owner,
            "proto.native.event.jungle_maze_adventure",
            "JUNGLE_MAZE_ADVENTURE");
        NativeEventContextRngV111.Validate(
            owner,
            "proto.native.underdocks.sunken_treasury",
            "SUNKEN_TREASURY");
        NativeEventContextRngV111.Validate(
            owner,
            "proto.native.underdocks.sunken_statue",
            "SUNKEN_STATUE");
    }

    internal static RunStochasticStateV111 WithNativeRunRng(
        RunStochasticStateV111 current, RngBundle? nativeRunRng)
    {
        return current with { NativeRunRng = nativeRunRng };
    }

    internal static RunStochasticStateV111 WithReferenceNature(
        RunStochasticStateV111 current,
        ReferenceNatureStateV111? referenceNature)
    {
        return current with { ReferenceNature = referenceNature };
    }

    internal static RunStochasticStateV111 WithControlledEventChoiceHistory(
        RunStochasticStateV111 current,
        ControlledEventChoiceTraceV111[]? history) =>
        current with { ControlledEventChoiceHistory = history };

    internal static RunStochasticStateV111 WithControlledEventRewardChoice(
        RunStochasticStateV111 current,
        PrototypeControlledEventRewardChoiceContinuationV111? choice) =>
        current with { ControlledEventRewardChoice = choice };

    internal static RunStochasticStateV111 WithControlledRunActionHistory(
        RunStochasticStateV111 current,
        ControlledRunActionTraceV111[]? history) =>
        current with { ControlledRunActionHistory = history };

    internal static RunStochasticStateV111 WithControlledRunActionChoice(
        RunStochasticStateV111 current,
        PrototypeControlledRunActionContinuationV111? choice) =>
        current with { ControlledRunActionChoice = choice };

    internal static RunStochasticStateV111 WithPrototypeRng(
        RunStochasticStateV111 current, RngBundle prototypeRng) =>
        current with { PrototypeRng = prototypeRng };

    internal static RunStochasticStateV111 WithSemanticOtherChance(
        RunStochasticStateV111 current,
        SemanticOtherChanceStateV111? semanticOtherChance) =>
        current with { SemanticOtherChance = semanticOtherChance };

    internal static RunStochasticStateV111 WithSemanticRewardAudit(
        RunStochasticStateV111 current,
        SemanticRewardAuditV111? semanticRewardAudit) =>
        current with { SemanticRewardAudit = semanticRewardAudit };
}
