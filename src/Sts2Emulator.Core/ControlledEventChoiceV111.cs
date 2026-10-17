using System.Globalization;
using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Opt-in adapter for controlled stochastic event options. Ordinary engine
/// steps retain the legacy random profile; callers use this adapter to begin
/// or resume saved event-effect and event-reward continuations.
/// </summary>
public static class ControlledEventChoiceEngineV111
{
    public const string SchemaId = "sts2-v111-controlled-event-choice-v1";

    public static RunState Begin(RunState source, string choiceId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        if (source.Phase != RunPhase.Event
            || source.World?.Event is not { ControlledChoice: null } eventState
            || !PrototypeGameEngine.IsControlledEventChoice(
                eventState.EventId, choiceId))
            throw new InvalidOperationException(
                "Controlled event choice requires a supported option on the current event page.");
        var result = Step(source, GameAction.Create(
            "event_choice", new EventChoicePayload(choiceId)));
        if (result.World?.Event?.ControlledChoice is null)
            throw new NotSupportedException(
                "The selected event option did not reach a supported " +
                "controlled chance site.");
        return result;
    }

    public static RunState Resolve(RunState source, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.World?.Event?.ControlledChoice is null
            && source.ControlledEventRewardChoice is null)
            throw new InvalidOperationException(
                "No controlled event outcome is pending.");
        return Step(source, GameAction.Create(
            "choose_controlled_event_outcome",
            new ChooseControlledEventOutcomePayload(selectedIndex)));
    }

    public static RunState Advance(RunState source, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        PrototypeStateInvariants.Validate(source);
        var eventState = source.World?.Event;
        var reward = source.World?.Reward;
        var trace = source.ControlledEventChoiceHistory is { Length: > 0 } history
            ? history[^1]
            : null;
        if (source.Phase != RunPhase.Reward
            || source.ControlledEventRewardChoice is not null
            || eventState is null
            || eventState.ControlledChoice is not null
            || eventState.ChosenChoiceId is not { } choiceId
            || reward is null
            || reward.SourceRoom != "Event:" + eventState.EventId
            || !reward.AddRandomCurseAfterReward
            || !PrototypeGameEngine.IsControlledEventChoice(
                eventState.EventId, choiceId)
            || trace is null
            || trace.EventId != eventState.EventId
            || trace.ChoiceId != choiceId)
            throw new InvalidOperationException(
                "Controlled event-reward advance requires a completed supported event choice with a pending reward.");

        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(source).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "Controlled event-reward advance requires a current legal reward action.");

        using var guard = ControlledChanceGuardV111.Enter();
        var replay = ControlledChanceContinuationExecutorV111.Resume(
            source,
            [],
            checkpoint => engine.Step(checkpoint, action).State,
            OrdinalCounts(trace.Decisions));
        if (replay.PendingChance is { } pendingChance)
        {
            throw new NotSupportedException(
                "Controlled event chance site '" + pendingChance.Request.SiteId +
                "' (operation '" + pendingChance.Request.OperationKind +
                "') is outside the saved event-reward continuation.");
        }
        var result = replay.Run ?? throw new InvalidDataException(
            "Controlled event-reward advance returned neither a run nor a pending chance request.");
        PrototypeStateInvariants.Validate(result);
        return result;
    }

    private static IReadOnlyDictionary<string, int> OrdinalCounts(
        RandomIndexDecisionV111[] decisions) => decisions
        .GroupBy(decision => decision.Request.StreamId,
            StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(),
            StringComparer.Ordinal);

    private static RunState Step(RunState source, GameAction action)
    {
        using (ControlledChanceGuardV111.Enter())
        {
            var result = new PrototypeGameEngine().Step(source, action).State;
            PrototypeStateInvariants.Validate(result);
            return result;
        }
    }
}

/// <summary>
/// Completed controlled outcomes for one event option. This remains attached
/// to the run after its mechanics commit, so snapshots retain decision
/// provenance without retaining a replay checkpoint.
/// </summary>
public sealed record ControlledEventChoiceTraceV111(
    string EventId,
    string ChoiceId,
    RandomIndexDecisionV111[] Decisions,
    string[][] CandidateLabels,
    string ProbabilityStatus = "UNVERIFIED")
{
    public ControlledEventChoiceTraceV111 Fork() => this with
    {
        Decisions = Decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        CandidateLabels = CandidateLabels.Select(labels =>
            (string[])labels.Clone()).ToArray()
    };

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(EventId)
            || string.IsNullOrWhiteSpace(ChoiceId)
            || Decisions is null || CandidateLabels is null
            || Decisions.Length == 0
            || Decisions.Length != CandidateLabels.Length
            || !PrototypeGameEngine.IsControlledEventChoice(EventId, ChoiceId)
            || ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Malformed controlled event-choice transcript.");
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !ControlledEventChoiceReplayV111.IsEventChoiceSite(
                    decision.Request.SiteId)
                || CandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || CandidateLabels[index].Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled event-choice transcript contains an invalid outcome.");
        }
    }
}

/// <summary>
/// Suspended event-effect frame for a guarded option that needs a controlled
/// random outcome. Partial run state contains completed effects, and
/// NextEffectIndex identifies the effect that will resume.
/// </summary>
public sealed record PrototypeControlledEventChoiceContinuationV111(
    RunState PartialState,
    string EventId,
    string ChoiceId,
    int NextEffectIndex,
    int EffectDecisionStartIndex,
    string[] QueuedPotionIds,
    PrototypePendingEventDeckChoiceState[] QueuedDeckChoices,
    RewardState? DeferredReward,
    int DeferredHpLoss,
    int DeferredGoldLoss,
    string? DeferredCardId,
    RandomIndexDecisionV111[] Decisions,
    RandomIndexRequestV111 PendingRequest,
    string[]? PendingCandidateLabels = null,
    string[][]? DecisionCandidateLabels = null)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the suspended event effects.
    /// Existing continuation fields remain flattened snapshot projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<
        PrototypeControlledEventChoiceMechanicsStateV111,
        ControlledChanceContinuationStateV111> Simulation { get; init; } = new(
            new PrototypeControlledEventChoiceMechanicsStateV111(
                PartialState, EventId, ChoiceId, NextEffectIndex,
                EffectDecisionStartIndex, QueuedPotionIds,
                QueuedDeckChoices, DeferredReward, DeferredHpLoss,
                DeferredGoldLoss, DeferredCardId),
            new ControlledChanceContinuationStateV111(
                Decisions, PendingRequest, PendingCandidateLabels,
                DecisionCandidateLabels));

    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeControlledEventChoiceMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledChanceContinuationStateV111 Chance
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState PartialState
    {
        get => Mechanics.PartialState;
        init => Mechanics = Mechanics with { PartialState = value };
    }

    public string EventId
    {
        get => Mechanics.EventId;
        init => Mechanics = Mechanics with { EventId = value };
    }

    public string ChoiceId
    {
        get => Mechanics.ChoiceId;
        init => Mechanics = Mechanics with { ChoiceId = value };
    }

    public int NextEffectIndex
    {
        get => Mechanics.NextEffectIndex;
        init => Mechanics = Mechanics with { NextEffectIndex = value };
    }

    public int EffectDecisionStartIndex
    {
        get => Mechanics.EffectDecisionStartIndex;
        init => Mechanics = Mechanics with
        { EffectDecisionStartIndex = value };
    }

    public string[] QueuedPotionIds
    {
        get => Mechanics.QueuedPotionIds;
        init => Mechanics = Mechanics with { QueuedPotionIds = value };
    }

    public PrototypePendingEventDeckChoiceState[] QueuedDeckChoices
    {
        get => Mechanics.QueuedDeckChoices;
        init => Mechanics = Mechanics with { QueuedDeckChoices = value };
    }

    public RewardState? DeferredReward
    {
        get => Mechanics.DeferredReward;
        init => Mechanics = Mechanics with { DeferredReward = value };
    }

    public int DeferredHpLoss
    {
        get => Mechanics.DeferredHpLoss;
        init => Mechanics = Mechanics with { DeferredHpLoss = value };
    }

    public int DeferredGoldLoss
    {
        get => Mechanics.DeferredGoldLoss;
        init => Mechanics = Mechanics with { DeferredGoldLoss = value };
    }

    public string? DeferredCardId
    {
        get => Mechanics.DeferredCardId;
        init => Mechanics = Mechanics with { DeferredCardId = value };
    }

    public RandomIndexDecisionV111[] Decisions
    {
        get => Chance.Decisions;
        init => Chance = Chance with { Decisions = value };
    }

    public RandomIndexRequestV111 PendingRequest
    {
        get => Chance.PendingRequest;
        init => Chance = Chance with { PendingRequest = value };
    }

    public string[]? PendingCandidateLabels
    {
        get => Chance.PendingCandidateLabels;
        init => Chance = Chance with { PendingCandidateLabels = value };
    }

    public string[][]? DecisionCandidateLabels
    {
        get => Chance.DecisionCandidateLabels;
        init => Chance = Chance with { DecisionCandidateLabels = value };
    }

    public PrototypeControlledEventChoiceContinuationV111 Fork() => this with
    {
        PartialState = PartialState.Fork(),
        QueuedPotionIds = (string[])QueuedPotionIds.Clone(),
        QueuedDeckChoices = QueuedDeckChoices
            .Select(choice => choice.Fork()).ToArray(),
        DeferredReward = DeferredReward?.Fork(),
        Chance = Chance.Fork()
    };

    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ControlledEventChoice);
        if (PartialState is null || Decisions is null
            || DecisionCandidateLabels is null
            || DecisionCandidateLabels.Length != Decisions.Length
            || string.IsNullOrWhiteSpace(EventId)
            || string.IsNullOrWhiteSpace(ChoiceId)
            || QueuedPotionIds is null || QueuedDeckChoices is null
            || NextEffectIndex < 0
            || EffectDecisionStartIndex < 0
            || EffectDecisionStartIndex > Decisions.Length
            || PendingRequest is null)
            throw new InvalidDataException(
                "Malformed controlled event-choice continuation.");

        var partialEvent = PartialState.World?.Event;
        if (PartialState.Phase != RunPhase.Event
            || partialEvent is null
            || partialEvent.EventId != EventId
            || partialEvent.ChosenChoiceId is not null
            || partialEvent.ControlledChoice is not null
            || !PrototypeGameEngine.IsControlledEventChoice(EventId, ChoiceId))
            throw new InvalidDataException(
                "Controlled event-choice frame is outside its event option boundary.");
        var choice = PrototypeContent.Event(EventId).Choices.SingleOrDefault(
            item => item.Id == ChoiceId)
            ?? throw new InvalidDataException(
                "Controlled event-choice frame references an unknown option.");
        if (NextEffectIndex >= choice.Effects.Length)
            throw new InvalidDataException(
                "Controlled event-choice frame has no unresolved effect.");
        foreach (var potionId in QueuedPotionIds)
            _ = PrototypeContent.Potion(potionId);
        foreach (var queuedChoice in QueuedDeckChoices)
        {
            if (queuedChoice is null
                || queuedChoice.CandidateCardInstanceIds is null)
                throw new InvalidDataException(
                    "Controlled event-choice frame has a malformed queued deck choice.");
        }

        ValidateRequest(PendingRequest);
        if (PendingCandidateLabels is not null
            && (PendingCandidateLabels.Length != PendingRequest.SupportCount
                || PendingCandidateLabels.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException(
                "Controlled event-choice candidate labels do not match their ordered support.");
        if (PendingCandidateLabels is null)
            throw new InvalidDataException(
                "Controlled event-choice continuation is missing candidate labels.");
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null)
                throw new InvalidDataException(
                    "Controlled event-choice prefix contains a non-Nature outcome.");
            ValidateRequest(decision.Request);
            if (DecisionCandidateLabels[index] is null
                || DecisionCandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || DecisionCandidateLabels[index]
                    .Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled event-choice transcript labels do not match their ordered support.");
        }
        PrototypeStateInvariants.Validate(PartialState);
    }

    internal static RandomIndexRequestV111 CloneRequest(
        RandomIndexRequestV111 request) => request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };

    internal static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = CloneRequest(decision.Request)
    };

    private static void ValidateRequest(RandomIndexRequestV111 request)
    {
        request.Validate();
        if (!ControlledEventChoiceReplayV111.IsEventChoiceSite(request.SiteId))
            throw new InvalidDataException(
                "Controlled event-choice continuation contains an unrelated chance site.");
    }
}

/// <summary>
/// Suspended post-reward event chance. The partial state retains all player
/// reward decisions; the decision prefix identifies the already committed
/// event-effect outcomes that belong to the same event-choice transcript.
/// </summary>
public sealed record PrototypeControlledEventRewardChoiceContinuationV111(
    RunState PartialState,
    string EventId,
    string ChoiceId,
    int DecisionPrefixLength,
    RandomIndexDecisionV111[] Decisions,
    string[][] DecisionCandidateLabels,
    RandomIndexRequestV111 PendingRequest,
    string[] PendingCandidateLabels)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for a suspended post-reward event
    /// chance. Existing continuation fields remain flattened projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<
        PrototypeControlledEventRewardChoiceMechanicsStateV111,
        ControlledChanceContinuationStateV111> Simulation { get; init; } = new(
            new PrototypeControlledEventRewardChoiceMechanicsStateV111(
                PartialState, EventId, ChoiceId, DecisionPrefixLength),
            new ControlledChanceContinuationStateV111(
                Decisions, PendingRequest, PendingCandidateLabels,
                DecisionCandidateLabels));

    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeControlledEventRewardChoiceMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledChanceContinuationStateV111 Chance
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState PartialState
    {
        get => Mechanics.PartialState;
        init => Mechanics = Mechanics with { PartialState = value };
    }

    public string EventId
    {
        get => Mechanics.EventId;
        init => Mechanics = Mechanics with { EventId = value };
    }

    public string ChoiceId
    {
        get => Mechanics.ChoiceId;
        init => Mechanics = Mechanics with { ChoiceId = value };
    }

    public int DecisionPrefixLength
    {
        get => Mechanics.DecisionPrefixLength;
        init => Mechanics = Mechanics with { DecisionPrefixLength = value };
    }

    public RandomIndexDecisionV111[] Decisions
    {
        get => Chance.Decisions;
        init => Chance = Chance with { Decisions = value };
    }

    public string[][] DecisionCandidateLabels
    {
        get => Chance.DecisionCandidateLabels!;
        init => Chance = Chance with { DecisionCandidateLabels = value };
    }

    public RandomIndexRequestV111 PendingRequest
    {
        get => Chance.PendingRequest;
        init => Chance = Chance with { PendingRequest = value };
    }

    public string[] PendingCandidateLabels
    {
        get => Chance.PendingCandidateLabels!;
        init => Chance = Chance with { PendingCandidateLabels = value };
    }

    public PrototypeControlledEventRewardChoiceContinuationV111 Fork() => this with
    {
        PartialState = PartialState.Fork(),
        Chance = Chance.Fork()
    };

    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ControlledEventRewardChoice);
        if (PartialState is null || Decisions is null
            || DecisionCandidateLabels is null
            || DecisionCandidateLabels.Length != Decisions.Length
            || string.IsNullOrWhiteSpace(EventId)
            || string.IsNullOrWhiteSpace(ChoiceId)
            || DecisionPrefixLength < 1
            || DecisionPrefixLength > Decisions.Length
            || PendingRequest is null
            || PendingCandidateLabels is null)
            throw new InvalidDataException(
                "Malformed controlled event-reward continuation.");

        var world = PartialState.World;
        var eventState = world?.Event;
        var reward = world?.Reward;
        if (PartialState.Phase != RunPhase.Reward
            || PartialState.ControlledEventRewardChoice is not null
            || eventState is null
            || eventState.ControlledChoice is not null
            || eventState.EventId != EventId
            || eventState.ChosenChoiceId != ChoiceId
            || reward?.SourceRoom != "Event:" + EventId
            || !reward.AddRandomCurseAfterReward
            || !PrototypeGameEngine.IsControlledEventChoice(EventId, ChoiceId))
            throw new InvalidDataException(
                "Controlled event-reward frame is outside its post-reward boundary.");

        var history = PartialState.ControlledEventChoiceHistory;
        var trace = history is { Length: > 0 } ? history[^1] : null;
        if (trace is null
            || trace.EventId != EventId
            || trace.ChoiceId != ChoiceId
            || trace.Decisions.Length != DecisionPrefixLength
            || CanonicalJson.Sha256(trace.Decisions) !=
                CanonicalJson.Sha256(Decisions.Take(DecisionPrefixLength).ToArray())
            || CanonicalJson.Sha256(trace.CandidateLabels) !=
                CanonicalJson.Sha256(DecisionCandidateLabels
                    .Take(DecisionPrefixLength).ToArray()))
            throw new InvalidDataException(
                "Controlled event-reward frame has no matching committed event-choice prefix.");

        PendingRequest.Validate();
        if (PendingRequest.SiteId !=
                "event.reward.random_modifier_curse.index_draw"
            || PendingCandidateLabels.Length != PendingRequest.SupportCount
            || PendingCandidateLabels.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException(
                "Controlled event-reward candidate labels do not match their ordered support.");

        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !ControlledEventChoiceReplayV111.IsEventChoiceSite(
                    decision.Request.SiteId)
                || DecisionCandidateLabels[index] is null
                || DecisionCandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || DecisionCandidateLabels[index].Any(
                    string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled event-reward prefix contains an invalid outcome.");
        }

        PrototypeStateInvariants.Validate(PartialState);
    }

    internal IReadOnlyDictionary<string, int> PrefixOrdinalCounts() =>
        Decisions.Take(DecisionPrefixLength)
            .GroupBy(decision => decision.Request.StreamId,
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(),
                StringComparer.Ordinal);
}

internal sealed class ControlledEventChoiceChanceRequiredException(
    RandomIndexRequestV111 request,
    string[]? candidateLabels) : Exception(
        "A controlled event choice needs a random-choice outcome.")
{
    public RandomIndexRequestV111 Request { get; } =
        PrototypeControlledEventChoiceContinuationV111.CloneRequest(request);
    public string[]? CandidateLabels { get; } = candidateLabels is null
        ? null : (string[])candidateLabels.Clone();
}

/// <summary>
/// Supplies the current controlled outcome while the event-effect interpreter
/// resumes from its saved frame. The first unknown request suspends that
/// option with its exact ordered support.
/// </summary>
internal sealed class ControlledEventChoiceReplayV111 : IDisposable
{
    public const int MaximumMaterializedCandidateLabels = 4096;

    private static readonly AsyncLocal<ControlledEventChoiceReplayV111?> Active =
        new();
    private readonly ControlledEventChoiceReplayV111? _previous;
    private readonly ControlledRandomIndexReplayPrefixV111 _prefix;
    private readonly Dictionary<string, int> _priorOrdinalCounts;
    private string[]? _candidateLabels;
    private bool _disposed;

    public static bool IsActive => Active.Value is not null;

    private ControlledEventChoiceReplayV111(
        RandomIndexDecisionV111[] decisions,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts)
    {
        _previous = Active.Value;
        _prefix = new ControlledRandomIndexReplayPrefixV111(decisions);
        _priorOrdinalCounts = priorOrdinalCounts is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(priorOrdinalCounts,
                StringComparer.Ordinal);
        Active.Value = this;
    }

    public static ControlledEventChoiceReplayV111 Begin(
        RandomIndexDecisionV111[] decisions,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts = null)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new ControlledEventChoiceReplayV111(
            decisions, priorOrdinalCounts);
    }

    public static int Resolve(
        RandomIndexRequestV111 request,
        bool requestOrdinalIncludesCurrentOperationPrefix = false)
    {
        var active = Active.Value ?? throw new InvalidOperationException(
            "Controlled event-choice replay is not active.");
        request.Validate();
        if (!IsControlledReplaySite(request.SiteId))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + request.SiteId +
                " operation=" + request.OperationKind);

        var ordinal = checked(request.Ordinal +
            active._priorOrdinalCounts.GetValueOrDefault(request.StreamId) +
            active._prefix.Decisions
            .Take(active._prefix.ConsumedCount)
            .Count(decision => decision.Request.StreamId == request.StreamId
                && (!requestOrdinalIncludesCurrentOperationPrefix
                    || decision.Request.SiteId != request.SiteId
                    || decision.Request.OwnerId != request.OwnerId)));
        var normalizedRequest = request with { Ordinal = ordinal };
        var candidateLabels = active._candidateLabels;
        if (candidateLabels is null)
            candidateLabels = normalizedRequest.SupportCount
                <= MaximumMaterializedCandidateLabels
                ? Enumerable.Range(0, normalizedRequest.SupportCount).Select(index =>
                    index.ToString(CultureInfo.InvariantCulture)).ToArray()
                : Array.Empty<string>();
        if (candidateLabels.Length != 0
            && candidateLabels.Length != normalizedRequest.SupportCount)
            throw new InvalidDataException(
                "Event-choice candidate labels do not match the random support.");
        if (!active._prefix.TryConsume(
                normalizedRequest,
                "Event-choice replay diverged from its saved chance request.",
                out var selectedIndex))
            throw new ControlledEventChoiceChanceRequiredException(
                normalizedRequest, candidateLabels);
        return selectedIndex;
    }

    public void EnsureConsumed()
    {
        _prefix.EnsureConsumed(
            "Event-choice replay left saved controlled outcomes unused.");
    }

    public static IDisposable DescribeCandidates(string[]? candidateLabels)
    {
        var active = Active.Value;
        if (active is null)
            return EmptyScope.Instance;
        var previous = active._candidateLabels;
        active._candidateLabels = candidateLabels is null
            ? null : (string[])candidateLabels.Clone();
        return new CandidateLabelScope(active, previous);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Active.Value != this)
            throw new InvalidOperationException(
                "Controlled event-choice replay scopes must be disposed in reverse order.");
        Active.Value = _previous;
        _disposed = true;
    }

    private sealed class CandidateLabelScope(
        ControlledEventChoiceReplayV111 owner,
        string[]? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            owner._candidateLabels = previous;
            _disposed = true;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();
        public void Dispose() { }
    }

    public static bool IsEventChoiceSite(string siteId) =>
        StochasticSiteRegistryV111.IsControlledEventChoiceSite(siteId);

    public static bool IsRunActionSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionSite(siteId);

    public static bool IsRunActionEventEntrySite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionEventEntrySite(siteId);

    public static bool IsRunActionShopSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionShopSite(siteId);

    public static bool IsRunActionTreasureSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionTreasureSite(siteId);

    public static bool IsRunActionShovelSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionShovelSite(siteId);

    public static bool IsControlledReplaySite(string siteId) =>
        IsEventChoiceSite(siteId) || IsRunActionSite(siteId)
        || IsRunActionEventEntrySite(siteId)
        || IsRunActionShopSite(siteId)
        || IsRunActionTreasureSite(siteId)
        || IsRunActionShovelSite(siteId);
}

internal sealed class ControlledEventChoiceRandomIndexProviderV111
    : IRandomIndexProviderV111
{
    public string Mode => "controlled-v111";

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        if (!ControlledChanceGuardV111.Active
            || !ControlledEventChoiceReplayV111.IsActive)
            throw new InvalidOperationException(
                "Controlled event shuffle provider requires an active event frame.");
        var decision = new RandomIndexDecisionV111(
            PrototypeControlledEventChoiceContinuationV111.CloneRequest(request),
            ControlledEventChoiceReplayV111.Resolve(
                request, requestOrdinalIncludesCurrentOperationPrefix: true),
            Mode);
        decision.Validate();
        return decision;
    }
}
