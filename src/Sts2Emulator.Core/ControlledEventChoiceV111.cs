using System.Globalization;
using System.Threading;

namespace Sts2Emulator.Core;

/// <summary>
/// Immutable projection of the one active controlled event-choice boundary.
/// Event-effect and post-reward continuations share the same request shape,
/// labels, and stable boundary fingerprint through this descriptor.
/// </summary>
public sealed class ControlledEventChoicePendingV111
{
    private readonly ControlledChancePendingBoundaryV111 _chanceBoundary;

    public string EventId { get; }
    public string ChoiceId { get; }
    public string Kind { get; }
    public RandomIndexRequestV111 Request => _chanceBoundary.Request;
    public string[] CandidateLabels => _chanceBoundary.CandidateLabels;
    public string BoundaryFingerprint =>
        _chanceBoundary.BoundaryFingerprint;
    public string ChanceEventId => _chanceBoundary.ChanceEventId;

    private ControlledEventChoicePendingV111(
        string eventId, string choiceId, string kind,
        ControlledChancePendingBoundaryV111 chanceBoundary)
    {
        EventId = eventId;
        ChoiceId = choiceId;
        Kind = kind;
        _chanceBoundary = chanceBoundary ??
            throw new ArgumentNullException(nameof(chanceBoundary));
    }

    public static ControlledEventChoicePendingV111 Describe(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var effect = run.World?.Event?.Stochastic.ControlledChoice;
        var reward = run.Stochastic.ControlledEventRewardChoice;
        if ((effect is null) == (reward is null))
            throw new InvalidOperationException(
                "A controlled event-choice boundary requires exactly one pending request.");
        effect?.Validate();
        reward?.Validate();
        var eventId = effect?.EventId ?? reward!.EventId;
        var choiceId = effect?.ChoiceId ?? reward!.ChoiceId;
        var request = effect?.PendingRequest ?? reward!.PendingRequest;
        var candidateLabels = effect?.PendingCandidateLabels ??
            reward!.PendingCandidateLabels;
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidateLabels);
        var kind = effect is null
            ? "event_reward_random_choice" : "event_random_choice";
        var boundaryFingerprint = CanonicalJson.Sha256(new
        {
            schemaId = ControlledEventChoiceEngineV111.SchemaId,
            kind,
            eventId,
            choiceId,
            nextEffectIndex = effect?.NextEffectIndex,
            decisionPrefixLength = reward?.DecisionPrefixLength,
            request,
            candidateLabels
        });
        return new ControlledEventChoicePendingV111(
            eventId, choiceId, kind,
            ControlledChancePendingBoundaryV111.Create(
                ControlledEventChoiceEngineV111.SchemaId, request,
                candidateLabels, boundaryFingerprint));
    }
}

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
        if (eventState.EventId == PrototypeNativeStoneOfAllTime.EventId
            && (choiceId is "push" or "lift"))
        {
            var world = source.World;
            var nativeStreamId = NativeRngV111Contexts.EventStreamId(
                PrototypeNativeStoneOfAllTime.NativeRngEventId);
            var nativeRoute = source.Stochastic.NativeRunRng is not null
                && world is not null
                && PrototypeGameEngine.UsesNativeRegionalSystems(world)
                && eventState.Stochastic.NativeRng?.StreamId == nativeStreamId
                && eventState.Stochastic.ProviderMode is
                    null or "scripted-v111";
            var legacyRoute = source.Stochastic.NativeRunRng is null
                && world is not null
                && !PrototypeGameEngine.UsesNativeRegionalSystems(world)
                && eventState.Stochastic.NativeRng is null
                && eventState.Stochastic.ProviderMode is null;
            var nativeRegionalLegacyRoute = source.Stochastic.NativeRunRng is null
                && world is { Act: 2 } nativeRegionalWorld
                && PrototypeGameEngine.UsesNativeRegionalSystems(
                    nativeRegionalWorld)
                && eventState.Stochastic.NativeRng is null
                && eventState.Stochastic.ProviderMode is null;
            if (!nativeRoute && !legacyRoute && !nativeRegionalLegacyRoute)
                throw new NotSupportedException(
                    "ReferenceNature Stone of All Time requires a complete Native or legacy event RNG route.");
        }
        var result = Step(source, GameAction.Create(
            "event_choice", new EventChoicePayload(choiceId)));
        if (result.World?.Event?.Stochastic.ControlledChoice is null
            && !IsStonePushDeckChoiceBoundary(result)
            && !IsStonePushNoChanceTerminal(result, source, choiceId))
            throw new NotSupportedException(
                "The selected event option did not reach a supported " +
                "controlled chance site.");
        return result;
    }

    internal static bool IsStonePushDeckChoiceBoundary(RunState run)
    {
        var eventState = run.World?.Event;
        return run.Phase == RunPhase.Event
            && eventState is
            {
                EventId: PrototypeNativeStoneOfAllTime.EventId,
                ChosenChoiceId: "push",
                PendingDeckChoice: not null
            }
            && eventState.Stochastic.ControlledChoice is null
            && run.Stochastic.ControlledEventRewardChoice is null;
    }

    internal static bool IsStonePushNoChanceTerminal(
        RunState run, RunState origin, string choiceId) =>
        choiceId == "push"
        && origin.Phase == RunPhase.Event
        && origin.World?.Event?.EventId ==
            PrototypeNativeStoneOfAllTime.EventId
        && run.Phase == RunPhase.Terminal
        && run.World is { Event: null, TerminalOutcome: "defeat" };

    public static RunState Resolve(RunState source, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        var pending = ControlledEventChoicePendingV111.Describe(source);
        if ((uint)selectedIndex >= (uint)pending.Request.SupportCount)
            throw new ArgumentOutOfRangeException(nameof(selectedIndex));
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
        var engine = new PrototypeGameEngine();
        if (IsStonePushDeckChoiceBoundary(source))
        {
            var pushActionId = PrototypeAiEnvironment.StableActionId(action);
            if (!engine.GetLegalActions(source).Any(candidate =>
                    StringComparer.Ordinal.Equals(
                        PrototypeAiEnvironment.StableActionId(candidate),
                        pushActionId)))
                throw new InvalidOperationException(
                    "Controlled Stone of All Time advance requires a current legal deck-choice action.");

            var pushReplay = ControlledChanceGuardV111.ExecuteGuarded(() =>
                ControlledChanceContinuationExecutorV111.Resume(
                    source,
                    [],
                    checkpoint => engine.Step(checkpoint, action).State));
            if (pushReplay.PendingChance is { } pushChance)
                throw new NotSupportedException(
                    "Controlled Stone of All Time deck choice reached unsupported chance site '"
                    + pushChance.Request.SiteId + "'.");
            var pushResult = pushReplay.Run ?? throw new InvalidDataException(
                "Controlled Stone of All Time advance returned neither a run nor a pending chance request.");
            PrototypeStateInvariants.Validate(pushResult);
            return pushResult;
        }

        var trace = source.Stochastic.ControlledEventChoiceHistory is
            { Length: > 0 } history
            ? history[^1]
            : null;
        // Keep deterministic potion actions inside the saved event-reward
        // transcript while its reward is still pending.
        if (ControlledRunActionEngineV111
                .IsSupportedDeterministicEventRewardPotionAction(
                    source, action))
        {
            var eventTrace = trace ?? throw new InvalidDataException(
                "Event-reward potion action lost its completed event trace.");
            var potionActionId = PrototypeAiEnvironment.StableActionId(action);
            if (!engine.GetLegalActions(source).Any(candidate =>
                    StringComparer.Ordinal.Equals(
                        PrototypeAiEnvironment.StableActionId(candidate),
                        potionActionId)))
                throw new InvalidOperationException(
                    "Controlled event-reward potion use requires a current legal action.");

            var potionReplay = ControlledChanceGuardV111.ExecuteGuarded(() =>
                ControlledChanceContinuationExecutorV111.Resume(
                    source,
                    [],
                    checkpoint => engine.Step(checkpoint, action).State,
                    OrdinalCounts(eventTrace.Decisions)));
            if (potionReplay.PendingChance is { } potionChance)
                throw new NotSupportedException(
                    "Deterministic event-reward potion use reached unsupported chance site '"
                    + potionChance.Request.SiteId + "'.");
            var potionResult = potionReplay.Run ?? throw new InvalidDataException(
                "Controlled event-reward potion use returned neither a run nor a pending chance request.");
            PrototypeStateInvariants.Validate(potionResult);
            return potionResult;
        }
        if (source.Phase != RunPhase.Reward
            || source.Stochastic.ControlledEventRewardChoice is not null
            || eventState is null
            || eventState.Stochastic.ControlledChoice is not null
            || eventState.ChosenChoiceId is not { } choiceId
            || reward is null
            || reward.SourceRoom != "Event:" + eventState.EventId
            || !PrototypeGameEngine.IsControlledEventChoice(
                eventState.EventId, choiceId)
            || trace is null
            || trace.EventId != eventState.EventId
            || trace.ChoiceId != choiceId)
            throw new InvalidOperationException(
                "Controlled event-reward advance requires a completed supported event choice with a pending reward.");

        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(source).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "Controlled event-reward advance requires a current legal reward action.");

        var replay = ControlledChanceGuardV111.ExecuteGuarded(() =>
            ControlledChanceContinuationExecutorV111.Resume(
                source,
                [],
                checkpoint => StepEventRewardAction(checkpoint, action),
                OrdinalCounts(trace.Decisions)));
        if (replay.PendingChance is { } pendingChance)
        {
            var pendingLabels = pendingChance.CandidateLabels;
            var isRerollRequest = action.Kind == "reroll_reward_card"
                && ControlledEventChoiceReplayV111
                    .IsEventRewardRerollRequest(pendingChance.Request);
            var isEntropicBrewRequest =
                ControlledRunActionEngineV111
                    .IsSupportedEventRewardPotionAction(source, action)
                && ControlledRunActionEngineV111
                    .IsEntropicBrewFillRequest(
                        source, action, pendingChance.Request);
            var isModifierCurseRequest = action.Kind == "leave_reward"
                && pendingChance.Request.SiteId ==
                    "event.reward.random_modifier_curse.index_draw"
                && pendingChance.Request.StreamId == "reward"
                && pendingChance.Request.OperationKind == "choose_index";
            if ((!isRerollRequest && !isModifierCurseRequest
                    && !isEntropicBrewRequest)
                || pendingLabels is null
                || pendingLabels.Length !=
                    pendingChance.Request.SupportCount
                || pendingLabels.Any(string.IsNullOrWhiteSpace)
                || isEntropicBrewRequest
                    && !pendingLabels.SequenceEqual(
                        PrototypeContent.PotionPool,
                        StringComparer.Ordinal))
                throw new NotSupportedException(
                    "Controlled event chance site '" + pendingChance.Request.SiteId +
                    "' (operation '" + pendingChance.Request.OperationKind +
                    "') is outside the saved event-reward continuation.");

            var continuation =
                new PrototypeControlledEventRewardChoiceContinuationV111(
                    source.Fork(),
                    eventState.EventId,
                    choiceId,
                    trace.Decisions.Length,
                    trace.Decisions,
                    trace.CandidateLabels,
                    pendingChance.Request,
                    pendingLabels,
                    isRerollRequest || isEntropicBrewRequest
                        ? action with { Payload = action.Payload.Clone() }
                        : null);
            var suspended = RunStochasticStateV111
                .SuspendControlledEventRewardChoice(source, continuation);
            PrototypeStateInvariants.Validate(suspended);
            return suspended;
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

    internal static RunState StepEventRewardAction(
        RunState checkpoint, GameAction action)
    {
        if (ControlledRunActionEngineV111.IsEntropicBrewPotionAction(
                checkpoint, action))
        {
            using var candidateLabels =
                ControlledEventChoiceReplayV111.DescribeCandidates(
                    PrototypeContent.PotionPool);
            return new PrototypeGameEngine().Step(
                checkpoint, action).State;
        }

        return new PrototypeGameEngine().Step(checkpoint, action).State;
    }

    private static RunState Step(RunState source, GameAction action)
    {
        return ControlledChanceGuardV111.ExecuteGuarded(() =>
        {
            var result = new PrototypeGameEngine().Step(source, action).State;
            PrototypeStateInvariants.Validate(result);
            return result;
        });
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
    private RandomIndexDecisionV111[]? _decisions =
        CloneDecisions(Decisions);
    private string[][]? _candidateLabels = CloneCandidateLabels(
        CandidateLabels);

    public RandomIndexDecisionV111[] Decisions
    {
        get => CloneDecisions(_decisions)!;
        init => _decisions = CloneDecisions(value);
    }

    public string[][] CandidateLabels
    {
        get => CloneCandidateLabels(_candidateLabels)!;
        init => _candidateLabels = CloneCandidateLabels(value);
    }

    public ControlledEventChoiceTraceV111 Fork() => this with
    {
        Decisions = _decisions!,
        CandidateLabels = _candidateLabels!
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
        var sawEventChoice = false;
        var sawRewardReroll = false;
        var sawEntropicBrewFill = false;
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            var isRewardReroll =
                ControlledEventChoiceReplayV111
                    .IsEventRewardRerollRequest(decision.Request);
            var isEntropicBrewFill =
                ControlledRunActionEngineV111
                    .IsEntropicBrewFillRequestShape(decision.Request);
            var isEventChoice = decision.Request.SiteId ==
                    "run.potion.fill_empty_slots.model"
                ? isEntropicBrewFill
                : ControlledEventChoiceReplayV111
                    .IsEventChoiceSite(decision.Request.SiteId);
            var isFinalModifierCurse = decision.Request.SiteId ==
                    "event.reward.random_modifier_curse.index_draw"
                && decision.Request.StreamId == "reward"
                && decision.Request.OperationKind == "choose_index"
                && index == Decisions.Length - 1;
            var isSupportedSite = isRewardReroll
                ? sawEventChoice
                : isEntropicBrewFill
                    ? sawEventChoice
                    : (!sawRewardReroll && isEventChoice)
                    || (sawEventChoice &&
                        (sawRewardReroll || sawEntropicBrewFill)
                        && isFinalModifierCurse);
            if (!isRewardReroll && !sawRewardReroll && isEventChoice)
                sawEventChoice = true;
            sawRewardReroll |= isRewardReroll;
            sawEntropicBrewFill |= isEntropicBrewFill;
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !isSupportedSite
                || CandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || CandidateLabels[index].Any(string.IsNullOrWhiteSpace)
                || isEntropicBrewFill
                    && !ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                        CandidateLabels[index],
                        PrototypeContent.PotionPool))
                throw new InvalidDataException(
                    "Controlled event-choice transcript contains an invalid outcome.");
        }
        if (!sawEventChoice)
            throw new InvalidDataException(
                "Controlled event-choice transcript has no event-effect outcome.");
    }

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? decisions) => decisions?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();

    private static string[][]? CloneCandidateLabels(string[][]? labels) =>
        labels?.Select(candidateLabels => candidateLabels is null
            ? null! : (string[])candidateLabels.Clone()).ToArray();
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
        var partialWorld = PartialState.World;
        var chosenStonePush = EventId ==
                PrototypeNativeStoneOfAllTime.EventId
            && ChoiceId == "push"
            && partialEvent?.ChosenChoiceId == "push";
        if (PartialState.Phase != RunPhase.Event
            || partialEvent is null
            || partialEvent.EventId != EventId
            || (partialEvent.ChosenChoiceId is not null && !chosenStonePush)
            || partialEvent.Stochastic.ControlledChoice is not null
            || !PrototypeGameEngine.IsControlledEventChoice(EventId, ChoiceId))
            throw new InvalidDataException(
                "Controlled event-choice frame is outside its event option boundary.");
        var stoneEvent = partialEvent;
        var choice = PrototypeContent.Event(EventId).Choices.SingleOrDefault(
            item => item.Id == ChoiceId)
            ?? throw new InvalidDataException(
                "Controlled event-choice frame references an unknown option.");
        var expectedStoneSiteId = ChoiceId switch
        {
            "push" => ChanceLawEngineV111.NativeStoneOfAllTimePushTicketSiteId,
            "lift" => ChanceLawEngineV111.NativeStoneOfAllTimeLiftTicketSiteId,
            _ => null
        };
        var expectedStoneOwnerId = ChoiceId is "push" or "lift"
            ? $"event:stone_of_all_time:{ChoiceId}"
            : null;
        var expectedStoneStreamId = NativeRngV111Contexts.EventStreamId(
            PrototypeNativeStoneOfAllTime.NativeRngEventId);
        var nativeStoneRoute = PartialState.Stochastic.NativeRunRng is not null
            && partialWorld is { Act: 2 } nativeWorld
            && PrototypeGameEngine.UsesNativeRegionalSystems(nativeWorld)
            && stoneEvent.Stochastic.NativeRng?.StreamId ==
                expectedStoneStreamId
            && stoneEvent.Stochastic.ProviderMode is
                null or "scripted-v111";
        var legacyStoneRoute = PartialState.Stochastic.NativeRunRng is null
            && partialWorld is { Act: 2 } legacyWorld
            && !PrototypeGameEngine.UsesNativeRegionalSystems(legacyWorld)
            && stoneEvent.Stochastic.NativeRng is null
            && stoneEvent.Stochastic.ProviderMode is null;
        var nativeRegionalLegacyStoneRoute =
            PartialState.Stochastic.NativeRunRng is null
            && partialWorld is { Act: 2 } nativeRegionalWorld
            && PrototypeGameEngine.UsesNativeRegionalSystems(
                nativeRegionalWorld)
            && stoneEvent.Stochastic.NativeRng is null
            && stoneEvent.Stochastic.ProviderMode is null;
        var isStoneTicketRequest = EventId ==
                PrototypeNativeStoneOfAllTime.EventId
            && expectedStoneSiteId is not null
            && PendingRequest.SiteId == expectedStoneSiteId
            && PendingRequest.OwnerId == expectedStoneOwnerId
            && PendingRequest.OperationKind == "bounded_integer"
            && PendingRequest.BoundedCount == 100
            && (nativeStoneRoute
                && PendingRequest.StreamId == expectedStoneStreamId
                && PendingRequest.Ordinal == 1
                || legacyStoneRoute
                && PendingRequest.StreamId == "event"
                || nativeRegionalLegacyStoneRoute
                && PendingRequest.StreamId == "event");
        var hasStonePotion = stoneEvent.Stochastic.RandomOutcomes
            .NativeEventPotionSlot is
                { } potionSlot
            && potionSlot >= 0
            && potionSlot < PartialState.Player.PotionSlots.Length
            && PartialState.Player.PotionSlots[potionSlot] is not null;
        var isStoneLiftTicket = EventId ==
                PrototypeNativeStoneOfAllTime.EventId
            && ChoiceId == "lift"
            && choice.Effects.Length == 0
            && NextEffectIndex == 0
            && EffectDecisionStartIndex == 0
            && Decisions.Length == 0
            && QueuedPotionIds.Length == 0
            && QueuedDeckChoices.Length == 0
            && DeferredReward is null
            && DeferredHpLoss == 0
            && DeferredGoldLoss == 0
            && DeferredCardId is null
            && isStoneTicketRequest
            && stoneEvent.NativePageIndex == 0
            && stoneEvent.ChosenChoiceId is null
            && hasStonePotion;
        var isStonePushTicket = EventId ==
                PrototypeNativeStoneOfAllTime.EventId
            && ChoiceId == "push"
            && choice.Effects.Length == 1
            && NextEffectIndex == choice.Effects.Length
            && EffectDecisionStartIndex == 0
            && Decisions.Length == 0
            && QueuedPotionIds.Length == 0
            && QueuedDeckChoices.Length == 0
            && DeferredReward is null
            && DeferredHpLoss == 0
            && DeferredGoldLoss == 0
            && DeferredCardId is null
            && chosenStonePush
            && isStoneTicketRequest
            && stoneEvent.NativePageIndex == 0
            && stoneEvent.PendingDeckChoice is null
            && stoneEvent.PendingPotionReplacement is null
            && stoneEvent.RemainingDeckChoices.Length == 0
            && stoneEvent.RemainingPotionIds.Length == 0
            && stoneEvent.PendingReward is null
            && PartialState.Player.Hp > 0
            && hasStonePotion;
        if (NextEffectIndex >= choice.Effects.Length
                && !isStoneLiftTicket && !isStonePushTicket)
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
        if ((isStoneLiftTicket || isStonePushTicket) &&
            !ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                PendingCandidateLabels,
                Enumerable.Range(0, 100).Select(index =>
                    index.ToString(CultureInfo.InvariantCulture))))
            throw new InvalidDataException(
                "Stone of All Time ticket continuation has malformed labels.");
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
/// reward decisions; the decision prefix identifies committed event-effect
/// outcomes, while a saved reroll or Entropic Brew action can add its own
/// chance outcomes to the same event-choice transcript.
/// </summary>
public sealed record PrototypeControlledEventRewardChoiceContinuationV111(
    RunState PartialState,
    string EventId,
    string ChoiceId,
    int DecisionPrefixLength,
    RandomIndexDecisionV111[] Decisions,
    string[][] DecisionCandidateLabels,
    RandomIndexRequestV111 PendingRequest,
    string[] PendingCandidateLabels,
    GameAction? ResumeAction = null)
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
                PartialState, EventId, ChoiceId, DecisionPrefixLength,
                ResumeAction),
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

    public GameAction? ResumeAction
    {
        get => Mechanics.ResumeAction;
        init => Mechanics = Mechanics with
        {
            ResumeAction = value is null ? null : CloneAction(value)
        };
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
        ResumeAction = ResumeAction is null ? null : CloneAction(ResumeAction),
        Chance = Chance.Fork()
    };

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
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
            || PartialState.Stochastic.ControlledEventRewardChoice is not null
            || eventState is null
            || eventState.Stochastic.ControlledChoice is not null
            || eventState.EventId != EventId
            || eventState.ChosenChoiceId != ChoiceId
            || reward?.SourceRoom != "Event:" + EventId
            || !PrototypeGameEngine.IsControlledEventChoice(EventId, ChoiceId))
            throw new InvalidDataException(
                "Controlled event-reward frame is outside its post-reward boundary.");

        if (ResumeAction is null)
        {
            if (!reward.AddRandomCurseAfterReward
                || PendingRequest.SiteId !=
                    "event.reward.random_modifier_curse.index_draw"
                || PendingRequest.StreamId != "reward"
                || PendingRequest.OperationKind != "choose_index"
                || Decisions.Length != DecisionPrefixLength)
                throw new InvalidDataException(
                    "Controlled event-reward completion frame is outside its modifier-curse boundary.");
        }
        else
        {
            var actionId = PrototypeAiEnvironment.StableActionId(
                ResumeAction);
            var expectedOperation = PendingRequest.SiteId ==
                "reward.card_rarity.ticket"
                    ? "bounded_integer" : "choose_index";
            var isRerollAction = ResumeAction.Kind ==
                "reroll_reward_card"
                && ControlledEventChoiceReplayV111
                    .IsEventRewardRerollSite(PendingRequest.SiteId)
                && PendingRequest.StreamId == "reward"
                && PendingRequest.OperationKind == expectedOperation;
            var isEntropicBrewAction =
                ControlledRunActionEngineV111
                    .IsEntropicBrewFillRequest(
                        PartialState, ResumeAction, PendingRequest)
                && ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                    PendingCandidateLabels, PrototypeContent.PotionPool);
            if ((!isRerollAction && !isEntropicBrewAction)
                || !new PrototypeGameEngine().GetLegalActions(PartialState)
                    .Any(action => StringComparer.Ordinal.Equals(
                        PrototypeAiEnvironment.StableActionId(action),
                        actionId)))
                throw new InvalidDataException(
                    "Controlled event-reward action frame is outside a supported reroll or Entropic Brew boundary.");
        }

        var history = PartialState.Stochastic.ControlledEventChoiceHistory;
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
        if (PendingCandidateLabels.Length != PendingRequest.SupportCount
            || PendingCandidateLabels.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException(
                "Controlled event-reward candidate labels do not match their ordered support.");

        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            decision.Validate();
            var isCommittedPrefix = index < DecisionPrefixLength;
            var isEventChoicePrefixSite =
                decision.Request.SiteId ==
                    "run.potion.fill_empty_slots.model"
                    ? ControlledRunActionEngineV111
                        .IsEntropicBrewFillRequestShape(
                            decision.Request)
                    : ControlledEventChoiceReplayV111
                        .IsEventChoiceSite(decision.Request.SiteId);
            var isSupportedSite = isCommittedPrefix
                ? isEventChoicePrefixSite
                    || ControlledEventChoiceReplayV111
                        .IsEventRewardRerollRequest(decision.Request)
                : ResumeAction is { } resumeAction
                    && (ControlledEventChoiceReplayV111
                            .IsEventRewardRerollRequest(decision.Request)
                        || ControlledRunActionEngineV111
                            .IsEntropicBrewFillRequest(
                                PartialState, resumeAction,
                                decision.Request));
            var isEntropicBrewDecision = !isCommittedPrefix
                && ResumeAction is { } savedBrewAction
                        && ControlledRunActionEngineV111
                            .IsEntropicBrewFillRequest(
                                PartialState, savedBrewAction,
                                decision.Request);
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !isSupportedSite
                || DecisionCandidateLabels[index] is null
                || DecisionCandidateLabels[index].Length !=
                    decision.Request.SupportCount
                || DecisionCandidateLabels[index].Any(
                    string.IsNullOrWhiteSpace)
                || isEntropicBrewDecision
                    && !ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                        DecisionCandidateLabels[index],
                        PrototypeContent.PotionPool))
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
    string[]? candidateLabels) : ControlledChanceRequiredExceptionV111(
        request, candidateLabels,
        "A controlled event choice needs a random-choice outcome.");

/// <summary>
/// Supplies the current controlled outcome while the event-effect interpreter
/// resumes from its saved frame. The first unknown request suspends that
/// option with its exact ordered support.
/// </summary>
internal sealed class ControlledEventChoiceReplayV111 :
    IControlledChanceReplayScopeV111
{
    public const int MaximumMaterializedCandidateLabels = 4096;

    private static readonly AsyncLocal<ControlledEventChoiceReplayV111?> Active =
        new();
    private readonly ControlledEventChoiceReplayV111? _previous;
    private readonly ControlledStochasticChanceReplayV111 _prefix;
    private readonly Dictionary<string, int> _priorOrdinalCounts;
    private readonly bool _compareCandidateLabels;
    private string[]? _candidateLabels;
    private bool _disposed;

    public static bool IsActive => Active.Value is not null;

    private ControlledEventChoiceReplayV111(
        RandomIndexDecisionV111[] decisions,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts,
        string[][]? decisionCandidateLabels)
    {
        if (decisionCandidateLabels is not null &&
            decisionCandidateLabels.Length != decisions.Length)
            throw new InvalidDataException(
                "Event-choice candidate-label prefix has the wrong length.");
        _previous = Active.Value;
        _prefix = ControlledStochasticChanceReplayV111.BeginDetachedPrefix(
            decisions.Select((decision, index) =>
                ControlledStochasticDecisionV111.FromIndex(
                    decision,
                    decisionCandidateLabels is { } labels &&
                        labels[index] is { } candidateLabels
                        ? candidateLabels : null))
                .ToArray());
        _priorOrdinalCounts = priorOrdinalCounts is null
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(priorOrdinalCounts,
                StringComparer.Ordinal);
        _compareCandidateLabels = decisionCandidateLabels is not null;
        Active.Value = this;
    }

    public static ControlledEventChoiceReplayV111 Begin(
        RandomIndexDecisionV111[] decisions,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts = null,
        string[][]? decisionCandidateLabels = null)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new ControlledEventChoiceReplayV111(
            decisions, priorOrdinalCounts, decisionCandidateLabels);
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
            .Count(decision => decision.IndexDecision is { } indexDecision &&
                indexDecision.Request.StreamId == request.StreamId &&
                (!requestOrdinalIncludesCurrentOperationPrefix ||
                 indexDecision.Request.SiteId != request.SiteId ||
                 indexDecision.Request.OwnerId != request.OwnerId)));
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
        var typedRequest = ControlledStochasticRequestV111.FromIndex(
            normalizedRequest);
        ControlledStochasticDecisionV111 savedDecision;
        bool matched;
        if (active._compareCandidateLabels)
            matched = active._prefix.TryTake(
                typedRequest, candidateLabels, out savedDecision,
                "Event-choice replay diverged from its saved chance request.");
        else
            matched = active._prefix.TryTake(
                typedRequest, out savedDecision,
                "Event-choice replay diverged from its saved chance request.");
        if (!matched)
            throw new ControlledEventChoiceChanceRequiredException(
                normalizedRequest, candidateLabels);
        return savedDecision.IndexDecision?.SelectedIndex ??
            throw new InvalidDataException(
                "Event-choice replay consumed a non-index outcome.");
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
        _prefix.Dispose();
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

    public static bool IsEventRewardRerollSite(string siteId) =>
        siteId is "reward.card_rarity.ticket"
            or "reward.card_model.index_draw";

    public static bool IsEventRewardRerollRequest(
        RandomIndexRequestV111 request) =>
        IsEventRewardRerollSite(request.SiteId)
        && request.StreamId == "reward"
        && request.OperationKind == (request.SiteId ==
            "reward.card_rarity.ticket"
                ? "bounded_integer" : "choose_index");

    public static bool IsRunActionSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionSite(siteId);

    public static bool IsRunActionEventEntrySite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionEventEntrySite(siteId);

    public static bool IsRunActionShopSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionShopSite(siteId);

    public static bool IsRunActionShopPurchaseSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionShopPurchaseSite(siteId);

    public static bool IsRunActionTreasureSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionTreasureSite(siteId);

    public static bool IsRunActionShovelSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionShovelSite(siteId);

    public static bool IsRunActionCombatSite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionCombatSite(siteId);

    public static bool IsRunActionOvergrowthCombatEntrySite(string siteId) =>
        StochasticSiteRegistryV111
            .IsRunActionOvergrowthCombatEntrySite(siteId);

    public static bool IsRunActionUnderdocksCombatEntrySite(string siteId) =>
        StochasticSiteRegistryV111
            .IsRunActionUnderdocksCombatEntrySite(siteId);

    public static bool IsRunActionCombatEntryPotionSite(string siteId) =>
        StochasticSiteRegistryV111
            .IsRunActionCombatEntryPotionSite(siteId);

    public static bool IsRunActionCombatEntrySite(string siteId) =>
        StochasticSiteRegistryV111.IsRunActionCombatEntrySite(siteId);

    public static bool IsControlledReplaySite(string siteId) =>
        IsEventChoiceSite(siteId) || IsRunActionSite(siteId)
        || IsRunActionEventEntrySite(siteId)
        || IsRunActionShopSite(siteId)
        || IsRunActionShopPurchaseSite(siteId)
        || StochasticSiteRegistryV111
            .IsRunActionRestHealRewardSite(siteId)
        || IsRunActionTreasureSite(siteId)
        || IsRunActionCombatEntrySite(siteId)
        || IsRunActionCombatSite(siteId)
        || IsRunActionShovelSite(siteId);
}

internal sealed class ControlledEventChoiceRandomIndexProviderV111
    : IRandomIndexProviderV111
{
    private readonly bool _requestOrdinalIncludesCurrentOperationPrefix;

    public ControlledEventChoiceRandomIndexProviderV111(
        bool requestOrdinalIncludesCurrentOperationPrefix = true)
    {
        _requestOrdinalIncludesCurrentOperationPrefix =
            requestOrdinalIncludesCurrentOperationPrefix;
    }

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
                request, _requestOrdinalIncludesCurrentOperationPrefix),
            Mode);
        decision.Validate();
        return decision;
    }
}
