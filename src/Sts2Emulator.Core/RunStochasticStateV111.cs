using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Atomic run-level ownership of provider-private stochastic state. The
/// compatibility properties on <see cref="RunState"/> project this envelope
/// through their existing names; v4 snapshots serialize this owner directly.
/// RNG bundles are copied on assignment and exposed as defensive public
/// projections, while mutable views remain inside owner implementation code.
/// </summary>
public sealed record RunStochasticStateV111
{
    private RngBundle? _prototypeRng;
    private RngBundle? _nativeRunRng;
    private ControlledEventChoiceTraceV111[]? _controlledEventChoiceHistory;
    private ControlledRunActionTraceV111[]? _controlledRunActionHistory;
    private ReferenceNatureMapGenerationAuditV111[]?
        _referenceNatureMapGenerationHistory;
    private ReferenceNatureAncientOfferSessionSnapshotV111[]?
        _referenceNatureAncientOfferHistory;

    [JsonConstructor]
    public RunStochasticStateV111(
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
        SemanticRewardAuditV111? SemanticRewardAudit = null,
        ReferenceNatureRunInitializationTranscriptV111?
            ReferenceNatureRunInitializationTranscript = null,
        bool ReferenceNatureRunInitializationPending = false,
        ReferenceNatureMapGenerationAuditV111[]?
            ReferenceNatureMapGenerationHistory = null,
        ReferenceNatureAncientOfferSessionSnapshotV111[]?
            ReferenceNatureAncientOfferHistory = null)
    {
        this.PrototypeRng = PrototypeRng;
        this.NativeRunRng = NativeRunRng;
        this.ReferenceNature = ReferenceNature;
        _controlledEventChoiceHistory = CopyTranscript(
            ControlledEventChoiceHistory, trace => trace.Fork());
        this.ControlledEventRewardChoice = ControlledEventRewardChoice;
        _controlledRunActionHistory = CopyTranscript(
            ControlledRunActionHistory, trace => trace.Fork());
        this.ControlledRunActionChoice = ControlledRunActionChoice;
        this.SemanticOtherChance = SemanticOtherChance;
        this.SemanticRewardAudit = SemanticRewardAudit;
        this.ReferenceNatureRunInitializationTranscript =
            ReferenceNatureRunInitializationTranscript?.Fork();
        this.ReferenceNatureRunInitializationPending =
            ReferenceNatureRunInitializationPending;
        _referenceNatureMapGenerationHistory = CopyTranscript(
            ReferenceNatureMapGenerationHistory,
            audit => audit.Fork());
        _referenceNatureAncientOfferHistory = CopyTranscript(
            ReferenceNatureAncientOfferHistory, audit => audit.Fork());
    }

    public RngBundle PrototypeRng
    {
        get => _prototypeRng?.Fork()!;
        init => _prototypeRng = value?.Fork();
    }
    public RngBundle? NativeRunRng
    {
        get => _nativeRunRng?.Fork();
        init => _nativeRunRng = value?.Fork();
    }
    /// <summary>Private mutable view used by owner validation and forking.</summary>
    private RngBundle PrototypeRngMutable => _prototypeRng!;
    /// <summary>Private mutable view used by owner validation and forking.</summary>
    private RngBundle? NativeRunRngMutable => _nativeRunRng;
    public ReferenceNatureStateV111? ReferenceNature { get; init; }
    public ControlledEventChoiceTraceV111[]? ControlledEventChoiceHistory
    {
        get => CopyTranscript(_controlledEventChoiceHistory,
            trace => trace.Fork());
        init => _controlledEventChoiceHistory = CopyTranscript(value,
            trace => trace.Fork());
    }
    public PrototypeControlledEventRewardChoiceContinuationV111?
        ControlledEventRewardChoice { get; init; }
    public ControlledRunActionTraceV111[]? ControlledRunActionHistory
    {
        get => CopyTranscript(_controlledRunActionHistory,
            trace => trace.Fork());
        init => _controlledRunActionHistory = CopyTranscript(value,
            trace => trace.Fork());
    }
    public PrototypeControlledRunActionContinuationV111?
        ControlledRunActionChoice { get; init; }
    public SemanticOtherChanceStateV111? SemanticOtherChance { get; init; }
    public SemanticRewardAuditV111? SemanticRewardAudit { get; init; }
    public ReferenceNatureRunInitializationTranscriptV111?
        ReferenceNatureRunInitializationTranscript { get; init; }
    public bool ReferenceNatureRunInitializationPending { get; init; }
    public ReferenceNatureMapGenerationAuditV111[]?
        ReferenceNatureMapGenerationHistory
    {
        get => CopyTranscript(_referenceNatureMapGenerationHistory,
            audit => audit.Fork());
        init => _referenceNatureMapGenerationHistory = CopyTranscript(value,
            audit => audit.Fork());
    }
    /// <summary>
    /// Completed Nature offer sessions from configured experimental act
    /// transitions, retained so run snapshots can replay their decisions.
    /// </summary>
    public ReferenceNatureAncientOfferSessionSnapshotV111[]?
        ReferenceNatureAncientOfferHistory
    {
        get => CopyTranscript(_referenceNatureAncientOfferHistory,
            audit => audit.Fork());
        init => _referenceNatureAncientOfferHistory = CopyTranscript(value,
            audit => audit.Fork());
    }

    public RunStochasticStateV111 Fork() => this with
    {
        PrototypeRng = PrototypeRngMutable.Fork(),
        NativeRunRng = NativeRunRngMutable?.Fork(),
        ReferenceNature = ReferenceNature?.Fork(),
        ControlledEventChoiceHistory = CopyTranscript(
            _controlledEventChoiceHistory, trace => trace.Fork()),
        ControlledEventRewardChoice = ControlledEventRewardChoice?.Fork(),
        ControlledRunActionHistory = CopyTranscript(
            _controlledRunActionHistory, trace => trace.Fork()),
        ControlledRunActionChoice = ControlledRunActionChoice?.Fork(),
        SemanticOtherChance = SemanticOtherChance?.Fork(),
        ReferenceNatureRunInitializationTranscript =
            ReferenceNatureRunInitializationTranscript?.Fork(),
        ReferenceNatureMapGenerationHistory = CopyTranscript(
            _referenceNatureMapGenerationHistory,
            audit => audit.Fork()),
        ReferenceNatureAncientOfferHistory = CopyTranscript(
            _referenceNatureAncientOfferHistory, audit => audit.Fork())
    };

    internal RunStochasticStateV111 AppendControlledEventChoiceTrace(
        ControlledEventChoiceTraceV111 trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        return this with
        {
            ControlledEventChoiceHistory = AppendValidatedTrace(
                _controlledEventChoiceHistory, trace,
                entry => entry.Validate(), entry => entry.Fork())
        };
    }

    internal RunStochasticStateV111 ReplaceLatestControlledEventChoiceTrace(
        ControlledEventChoiceTraceV111 trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        trace.Validate();
        var history = _controlledEventChoiceHistory ?? [];
        if (history.Length == 0 || history.Any(entry => entry is null) ||
            history[^1].EventId != trace.EventId ||
            history[^1].ChoiceId != trace.ChoiceId)
            throw new InvalidDataException(
                "Controlled event-choice replacement does not match the latest trace.");
        foreach (var entry in history)
            entry.Validate();
        var updated = (ControlledEventChoiceTraceV111[])history.Clone();
        updated[^1] = trace.Fork();
        return this with { ControlledEventChoiceHistory = updated };
    }

    internal RunStochasticStateV111 CompleteControlledRunActionTrace(
        ControlledRunActionTraceV111 trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        return this with
        {
            ControlledRunActionHistory = AppendValidatedTrace(
                _controlledRunActionHistory, trace,
                entry => entry.Validate(), entry => entry.Fork()),
            ControlledRunActionChoice = null
        };
    }

    /// <summary>
    /// Rebase suspended run continuations after the enclosing step has
    /// published all mechanics and provider updates. Each saved partial run
    /// is the same owner with only its own continuation removed, so replay
    /// compares against the complete committed state.
    /// </summary>
    internal static RunState RebasePendingContinuationCheckpoints(
        RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var current = owner;
        var stochastic = owner.Stochastic;

        if (stochastic.ControlledEventRewardChoice is
            { } rewardContinuation)
        {
            var partialState = current with
            {
                Stochastic = stochastic with
                {
                    ControlledEventRewardChoice = null
                }
            };
            current = SuspendControlledEventRewardChoice(
                partialState, rewardContinuation with
                {
                    PartialState = partialState
                });
            stochastic = current.Stochastic;
        }

        if (stochastic.ControlledRunActionChoice is
            { } runActionContinuation)
        {
            var partialState = current with
            {
                Stochastic = stochastic with
                {
                    ControlledRunActionChoice = null
                }
            };
            current = SuspendControlledRunActionChoice(
                partialState, runActionContinuation with
                {
                    PartialState = partialState
                });
        }

        return current;
    }

    internal static RunState SuspendControlledRunActionChoice(
        RunState partialState,
        PrototypeControlledRunActionContinuationV111 continuation)
    {
        ArgumentNullException.ThrowIfNull(partialState);
        ArgumentNullException.ThrowIfNull(continuation);
        continuation.Validate();
        if (partialState.Stochastic.ControlledRunActionChoice is not null ||
            CanonicalJson.Sha256(partialState) !=
                CanonicalJson.Sha256(continuation.PartialState))
            throw new InvalidDataException(
                "Controlled run-action continuation does not match its partial run owner.");
        var suspended = partialState with
        {
            Stochastic = WithControlledRunActionChoice(
                partialState.Stochastic, continuation)
        };
        suspended.Stochastic.ValidateControlledRunActionChoice(suspended);
        return suspended;
    }

    internal static RunState SuspendControlledEventRewardChoice(
        RunState partialState,
        PrototypeControlledEventRewardChoiceContinuationV111 continuation)
    {
        ArgumentNullException.ThrowIfNull(partialState);
        ArgumentNullException.ThrowIfNull(continuation);
        continuation.Validate();
        if (partialState.Stochastic.ControlledEventRewardChoice is not null ||
            CanonicalJson.Sha256(partialState) !=
                CanonicalJson.Sha256(continuation.PartialState))
            throw new InvalidDataException(
                "Controlled event-reward continuation does not match its partial run owner.");
        var suspended = partialState with
        {
            Stochastic = WithControlledEventRewardChoice(
                partialState.Stochastic, continuation)
        };
        suspended.Stochastic.ValidateControlledEventRewardChoice(suspended);
        return suspended;
    }

    internal RunStochasticStateV111 AppendReferenceNatureMapGenerationAudit(
        ReferenceNatureMapGenerationAuditV111 audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        audit.Validate();
        var history = _referenceNatureMapGenerationHistory ?? [];
        foreach (var previous in history)
        {
            if (previous is null)
                throw new InvalidDataException(
                    "ReferenceNature map history contains a null audit.");
            previous.Validate();
        }
        return this with
        {
            ReferenceNatureMapGenerationHistory = history
                .Select(previous => previous.Fork())
                .Append(audit.Fork()).ToArray()
        };
    }

    internal RunStochasticStateV111 AppendReferenceNatureAncientOfferAudit(
        ReferenceNatureAncientOfferSessionSnapshotV111 audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        var completed = ReferenceNatureAncientOfferSessionV111.Restore(audit);
        if (!completed.Complete)
            throw new InvalidDataException(
                "ReferenceNature Ancient-offer audit is incomplete.");
        var history = _referenceNatureAncientOfferHistory ?? [];
        foreach (var previous in history)
        {
            if (previous is null)
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer history contains a null audit.");
            if (!ReferenceNatureAncientOfferSessionV111.Restore(previous)
                    .Complete)
                throw new InvalidDataException(
                    "ReferenceNature Ancient-offer history contains an incomplete audit.");
        }
        return this with
        {
            ReferenceNatureAncientOfferHistory = history
                .Select(previous => previous.Fork())
                .Append(audit.Fork()).ToArray()
        };
    }

    public void Deconstruct(
        out RngBundle PrototypeRng,
        out RngBundle? NativeRunRng,
        out ReferenceNatureStateV111? ReferenceNature,
        out ControlledEventChoiceTraceV111[]? ControlledEventChoiceHistory,
        out PrototypeControlledEventRewardChoiceContinuationV111?
            ControlledEventRewardChoice,
        out ControlledRunActionTraceV111[]? ControlledRunActionHistory,
        out PrototypeControlledRunActionContinuationV111?
            ControlledRunActionChoice,
        out SemanticOtherChanceStateV111? SemanticOtherChance,
        out SemanticRewardAuditV111? SemanticRewardAudit)
    {
        PrototypeRng = this.PrototypeRng;
        NativeRunRng = this.NativeRunRng;
        ReferenceNature = this.ReferenceNature;
        ControlledEventChoiceHistory = this.ControlledEventChoiceHistory;
        ControlledEventRewardChoice = this.ControlledEventRewardChoice;
        ControlledRunActionHistory = this.ControlledRunActionHistory;
        ControlledRunActionChoice = this.ControlledRunActionChoice;
        SemanticOtherChance = this.SemanticOtherChance;
        SemanticRewardAudit = this.SemanticRewardAudit;
    }

    public void Validate(RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        global::Sts2Emulator.Core.PrototypeRng.ValidateBundle(
            PrototypeRngMutable,
            allowEmpty: owner.Phase == RunPhase.RunStart);
        ValidateControlledRunActionChoice(owner);
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
        NativeRunRngV111.Validate(owner, NativeRunRngMutable);
        ReferenceNature?.Validate();
        if (ReferenceNatureRunInitializationPending)
        {
            var history = ControlledRunActionHistory;
            var world = owner.World;
            var worldStochastic = world?.Stochastic;
            if (owner.Configuration?.UsesExperimentalSilentThreeActRoute != true ||
                world?.Act != 1 ||
                worldStochastic?.PreparedLaterActEncounterBags is not null ||
                worldStochastic?.LaterActEncounterBag is not null ||
                ReferenceNatureRunInitializationTranscript is not null ||
                ControlledRunActionChoice is not null ||
                history is not { Length: > 0 } ||
                history[^1].ActionId != "start_run")
                throw new InvalidDataException(
                    "Pending ReferenceNature run initialization differs from its experimental start boundary.");
        }
        if (SemanticOtherChance is not null)
            SemanticOtherChanceEngineV111.Validate(SemanticOtherChance);
        if (ReferenceNatureRunInitializationTranscript is { } runStart)
        {
            runStart.Validate();
            var expectedBossSite = owner.Configuration?.Acts[0] switch
            {
                ActIdentity.Overgrowth =>
                    "run.encounter.overgrowth_boss.index_draw",
                ActIdentity.Underdocks =>
                    PrototypeNativeUnderdocks.BossEncounterIndexSiteId,
                _ => null
            };
            var laws = runStart.ChanceLaws;
            var mapGeneration = runStart.ActOneMapGeneration;
            var startsWithMap = laws.Length > 1 &&
                laws[1].SiteId.StartsWith("map.overgrowth.",
                    StringComparison.Ordinal);
            if (ReferenceNature is not { } currentNature ||
                expectedBossSite is null || laws.Length == 0 ||
                laws[0].SiteId != expectedBossSite ||
                startsWithMap != (mapGeneration is not null) ||
                !StringComparer.Ordinal.Equals(
                    currentNature.Seed, runStart.FinalNatureState.Seed) ||
                currentNature.SampleCount <
                    runStart.FinalNatureState.SampleCount ||
                (currentNature.SampleCount ==
                    runStart.FinalNatureState.SampleCount &&
                 !currentNature.SameCursor(runStart.FinalNatureState)))
                throw new InvalidDataException(
                    "ReferenceNature cursor precedes or conflicts with its run-initialization transcript.");
            if (mapGeneration is not null &&
                (owner.Configuration?.Acts[0] != ActIdentity.Overgrowth ||
                 mapGeneration.Seed != owner.RunSeed ||
                 mapGeneration.Act != 1 ||
                 mapGeneration.Ascension != owner.Ascension ||
                 owner.World is not { } world ||
                 world.Act == 1 && world.Floor == 0 &&
                    world.Map.CurrentNodeId is null &&
                    CanonicalJson.Sha256(world.Map) !=
                        mapGeneration.MapFingerprint))
                throw new InvalidDataException(
                    "Overgrowth map audit does not match the configured run boundary or committed map.");
        }
        if (ReferenceNatureMapGenerationHistory is { Length: > 0 } mapHistory)
        {
            if (mapHistory.Length > 2 ||
                owner.Configuration is not
                    { UsesExperimentalSilentThreeActRoute: true,
                      CharacterId: "silent" } ||
                owner.World is not { } currentWorld ||
                ReferenceNature is not { } currentNature)
                throw new InvalidDataException(
                    "Later-act map audit history requires a configured experimental run with a ReferenceNature cursor.");
            var previousAct = 1;
            ReferenceNatureStateV111? previousMapCursor = null;
            foreach (var audit in mapHistory)
            {
                if (audit is null || audit.Act is < 2 or > 3 ||
                    audit.Act <= previousAct || audit.Act > currentWorld.Act ||
                    audit.Seed != owner.RunSeed ||
                    audit.Ascension != owner.Ascension)
                    throw new InvalidDataException(
                        "Later-act map audit history differs from its run or act order.");
                audit.Validate();
                if (previousMapCursor is { } previousCursor &&
                    (!StringComparer.Ordinal.Equals(
                        audit.InitialNatureState.Seed,
                        previousCursor.Seed) ||
                     audit.InitialNatureState.SampleCount <
                        previousCursor.SampleCount ||
                     audit.InitialNatureState.SampleCount ==
                        previousCursor.SampleCount &&
                     !audit.InitialNatureState.SameCursor(previousCursor)))
                    throw new InvalidDataException(
                        "Later-act map audits do not follow the ReferenceNature cursor chronology.");
                previousAct = audit.Act;
                previousMapCursor = audit.NatureState;
            }
            var finalMapCursor = mapHistory[^1].NatureState;
            if (!StringComparer.Ordinal.Equals(
                    currentNature.Seed, finalMapCursor.Seed) ||
                currentNature.SampleCount < finalMapCursor.SampleCount ||
                currentNature.SampleCount == finalMapCursor.SampleCount &&
                !currentNature.SameCursor(finalMapCursor))
                throw new InvalidDataException(
                    "ReferenceNature cursor precedes or conflicts with its later-act map audit history.");
        }
        if (ReferenceNatureAncientOfferHistory is { Length: > 0 }
            ancientOfferHistory)
        {
            var mapHistory = ReferenceNatureMapGenerationHistory;
            if (ancientOfferHistory.Length > 2 || mapHistory is not
                    { Length: > 0 } ||
                owner.Configuration is not
                    { UsesExperimentalSilentThreeActRoute: true,
                      CharacterId: "silent" } ||
                owner.World is not { } currentWorld ||
                ReferenceNature is not { } currentNature)
                throw new InvalidDataException(
                    "Ancient-offer audits require a configured experimental run with map audits and a ReferenceNature cursor.");
            var previousAct = 1;
            foreach (var audit in ancientOfferHistory)
            {
                if (audit is null)
                    throw new InvalidDataException(
                        "Ancient-offer audit history contains a null entry.");
                var session = ReferenceNatureAncientOfferSessionV111.Restore(
                    audit);
                var origin = session.Origin;
                var originWorld = origin.World;
                var act = originWorld?.Act ?? 0;
                var assignedAncientId = originWorld?.Stochastic
                    .LaterActEncounterBag?
                    .AssignedAncientId;
                var mappedSiteId = ReferenceNatureAncientOfferSessionV111
                    .SiteForAssignedAncientId(assignedAncientId);
                var mapAudit = mapHistory.SingleOrDefault(
                    candidate => candidate.Act == act);
                if (!session.Complete || act is < 2 or > 3 ||
                    act <= previousAct || act > currentWorld.Act ||
                    origin.RunSeed != owner.RunSeed ||
                    origin.Ascension != owner.Ascension ||
                    CanonicalJson.Sha256(origin.Configuration) !=
                        CanonicalJson.Sha256(owner.Configuration) ||
                    mappedSiteId != session.SiteId ||
                    originWorld?.Event is not null ||
                    originWorld?.Map.CurrentNodeId is not null ||
                    origin.Phase != RunPhase.MapChoice || mapAudit is null ||
                    CanonicalJson.Sha256(originWorld!.Map) !=
                        mapAudit.MapFingerprint ||
                    !session.InitialNatureState.SameCursor(
                        mapAudit.NatureState) ||
                    !session.InitialNatureState.SameCursor(
                        origin.Stochastic.ReferenceNature) ||
                    !session.NatureState.SameCursor(
                        session.Run.Stochastic.ReferenceNature) ||
                    session.Run.World?.Event?.EventId != assignedAncientId)
                    throw new InvalidDataException(
                        "Ancient-offer audit differs from its configured map boundary or Nature cursor.");
                var nextMapAudit = mapHistory.FirstOrDefault(
                    candidate => candidate.Act > act);
                if (nextMapAudit is not null &&
                    (!StringComparer.Ordinal.Equals(
                        nextMapAudit.InitialNatureState.Seed,
                        session.NatureState.Seed) ||
                     nextMapAudit.InitialNatureState.SampleCount <
                        session.NatureState.SampleCount ||
                     nextMapAudit.InitialNatureState.SampleCount ==
                        session.NatureState.SampleCount &&
                     !nextMapAudit.InitialNatureState.SameCursor(
                         session.NatureState)))
                    throw new InvalidDataException(
                        "Ancient-offer audits do not precede the next act's map cursor.");
                if (!StringComparer.Ordinal.Equals(
                        currentNature.Seed, session.NatureState.Seed) ||
                    currentNature.SampleCount < session.NatureState.SampleCount ||
                    currentNature.SampleCount ==
                        session.NatureState.SampleCount &&
                    !currentNature.SameCursor(session.NatureState))
                    throw new InvalidDataException(
                        "ReferenceNature cursor precedes or conflicts with its Ancient-offer audit history.");
                previousAct = act;
            }
        }
        if (SemanticRewardAudit is { } reward &&
            (owner.Phase != RunPhase.Reward ||
             reward.ProviderId != SemanticRewardAuditV111.PrototypeProviderId ||
             reward.ProbabilityStatus != "UNVERIFIED" ||
             reward.AfterCalls < reward.BeforeCalls ||
             PrototypeRngMutable.GetStream("reward").CallCount != reward.AfterCalls))
            throw new InvalidDataException(
                "Invalid source-timed reward RNG audit.");
        if (owner.World is { } world)
            world.Stochastic.Validate(world, owner);
    }

    private void ValidateControlledRunActionChoice(RunState owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (ControlledRunActionChoice is not { } controlledRunActionChoice)
            return;
        controlledRunActionChoice.Validate();
        var resumedOwner = owner with
        {
            Stochastic = owner.Stochastic with
            {
                ControlledRunActionChoice = null
            }
        };
        if (owner.DecisionIndex <
                controlledRunActionChoice.PartialState.DecisionIndex
            || CanonicalJson.Sha256(resumedOwner) !=
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
        var resumedOwner = owner with
        {
            Stochastic = owner.Stochastic with
            {
                ControlledEventRewardChoice = null
            }
        };
        if (world?.Event?.Stochastic.ControlledChoice is not null
            || owner.Phase != RunPhase.Reward
            || rewardEvent is null
            || rewardEvent.EventId != controlledEventRewardChoice.EventId
            || rewardEvent.ChosenChoiceId !=
                controlledEventRewardChoice.ChoiceId
            || reward?.SourceRoom !=
                "Event:" + controlledEventRewardChoice.EventId
            || (reward?.AddRandomCurseAfterReward != true
                && controlledEventRewardChoice.ResumeAction is null)
            || CanonicalJson.Sha256(resumedOwner) !=
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
        ArgumentNullException.ThrowIfNull(current);
        return referenceNature is null
            ? ClearReferenceNatureCursor(current)
            : CommitReferenceNatureCursor(current, referenceNature);
    }

    internal static RunStochasticStateV111 ClearReferenceNatureCursor(
        RunStochasticStateV111 current)
    {
        ArgumentNullException.ThrowIfNull(current);
        return current with { ReferenceNature = null };
    }

    internal static RunStochasticStateV111 CommitReferenceNatureCursor(
        RunStochasticStateV111 current,
        ReferenceNatureStateV111 cursor)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(cursor);
        cursor.Validate();
        if (current.ReferenceNature is { } previous &&
            (!StringComparer.Ordinal.Equals(previous.Seed, cursor.Seed) ||
             cursor.SampleCount < previous.SampleCount ||
             (cursor.SampleCount == previous.SampleCount &&
              !cursor.SameCursor(previous))))
            throw new InvalidDataException(
                "ReferenceNature cursor commit rewinds or replaces the run's committed cursor.");
        return current with { ReferenceNature = cursor.Fork() };
    }

    internal static RunStochasticStateV111 CommitReferenceNatureRunInitialization(
        RunStochasticStateV111 current,
        ReferenceNatureStateV111? natureState,
        ReferenceNatureRunInitializationTranscriptV111? transcript,
        bool pending)
    {
        ArgumentNullException.ThrowIfNull(current);
        natureState?.Validate();
        transcript?.Validate();
        if (pending && transcript is not null)
            throw new InvalidDataException(
                "Pending ReferenceNature run initialization cannot have a completed transcript.");
        if (transcript is { } completed &&
            (natureState is not { } committedCursor ||
             !StringComparer.Ordinal.Equals(
                 committedCursor.Seed, completed.FinalNatureState.Seed) ||
             committedCursor.SampleCount <
                 completed.FinalNatureState.SampleCount ||
             (committedCursor.SampleCount ==
                  completed.FinalNatureState.SampleCount &&
              !committedCursor.SameCursor(
                  completed.FinalNatureState))))
            throw new InvalidDataException(
                "ReferenceNature run initialization transcript differs from its committed cursor.");

        var committed = natureState is null
            ? ClearReferenceNatureCursor(current)
            : CommitReferenceNatureCursor(current, natureState);
        return committed with
        {
            ReferenceNatureRunInitializationTranscript = transcript?.Fork(),
            ReferenceNatureRunInitializationPending = pending
        };
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

    private static RunStochasticStateV111 PublishSemanticOtherChance(
        RunStochasticStateV111 mechanicsResult,
        RunStochasticStateV111 source,
        SemanticOtherChanceStateV111? updated = null)
    {
        ArgumentNullException.ThrowIfNull(mechanicsResult);
        ArgumentNullException.ThrowIfNull(source);
        var provider = updated ?? source.SemanticOtherChance;
        if (provider is not null)
            SemanticOtherChanceEngineV111.Validate(provider);
        return mechanicsResult with { SemanticOtherChance = provider };
    }

    internal static RunState PublishSemanticPilotContinuation(
        RunState mechanicsResult,
        RunState source,
        SemanticOtherChanceStateV111? updatedOtherChance,
        SemanticRewardAuditV111? rewardAudit)
    {
        ArgumentNullException.ThrowIfNull(mechanicsResult);
        ArgumentNullException.ThrowIfNull(source);
        var stochastic = PublishSemanticOtherChance(
            mechanicsResult.Stochastic, source.Stochastic,
            updatedOtherChance);
        stochastic = WithSemanticRewardAudit(stochastic, rewardAudit);
        return mechanicsResult with { Stochastic = stochastic };
    }

    internal static RunStochasticStateV111 WithSemanticRewardAudit(
        RunStochasticStateV111 current,
        SemanticRewardAuditV111? semanticRewardAudit) =>
        current with { SemanticRewardAudit = semanticRewardAudit };

    internal static RunStochasticStateV111
        WithReferenceNatureRunInitializationTranscript(
            RunStochasticStateV111 current,
            ReferenceNatureRunInitializationTranscriptV111? transcript) =>
        current with
        {
            ReferenceNatureRunInitializationTranscript = transcript?.Fork()
        };

    internal static RunStochasticStateV111
        WithReferenceNatureRunInitializationPending(
            RunStochasticStateV111 current, bool pending) =>
        current with { ReferenceNatureRunInitializationPending = pending };

    internal static RunStochasticStateV111
        WithReferenceNatureMapGenerationHistory(
            RunStochasticStateV111 current,
            ReferenceNatureMapGenerationAuditV111[]? history) =>
        current with
        {
            ReferenceNatureMapGenerationHistory = CopyTranscript(history,
                audit => audit.Fork())
        };

    internal static RunStochasticStateV111
        WithReferenceNatureAncientOfferHistory(
            RunStochasticStateV111 current,
            ReferenceNatureAncientOfferSessionSnapshotV111[]? history) =>
        current with
        {
            ReferenceNatureAncientOfferHistory = CopyTranscript(history,
                audit => audit.Fork())
        };

    private static T[]? CopyTranscript<T>(T[]? values, Func<T, T> clone)
        where T : class => values?.Select(value =>
            value is null ? null! : clone(value)).ToArray();

    private static T[] AppendValidatedTrace<T>(
        T[]? transcript, T entry, Action<T> validate, Func<T, T> fork)
        where T : class
    {
        validate(entry);
        var prefix = transcript ?? [];
        foreach (var previous in prefix)
        {
            if (previous is null)
                throw new InvalidDataException(
                    "Stochastic history contains a null trace.");
            validate(previous);
        }
        return prefix.Select(fork).Append(fork(entry)).ToArray();
    }
}
