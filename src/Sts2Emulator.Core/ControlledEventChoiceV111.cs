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

        try
        {
            using var guard = ControlledChanceGuardV111.Enter();
            using var replay = ControlledEventChoiceReplayV111.Begin(
                [], OrdinalCounts(trace.Decisions));
            var result = engine.Step(source, action).State;
            replay.EnsureConsumed();
            PrototypeStateInvariants.Validate(result);
            return result;
        }
        catch (ControlledEventChoiceChanceRequiredException exception)
        {
            throw new NotSupportedException(
                "Controlled event chance site '" + exception.Request.SiteId +
                "' (operation '" + exception.Request.OperationKind +
                "') is outside the saved event-reward continuation.");
        }
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
    public PrototypeControlledEventChoiceContinuationV111 Fork() => this with
    {
        PartialState = PartialState.Fork(),
        QueuedPotionIds = (string[])QueuedPotionIds.Clone(),
        QueuedDeckChoices = QueuedDeckChoices
            .Select(choice => choice.Fork()).ToArray(),
        DeferredReward = DeferredReward?.Fork(),
        Decisions = Decisions.Select(CloneDecision).ToArray(),
        PendingRequest = CloneRequest(PendingRequest),
        PendingCandidateLabels = PendingCandidateLabels is null
            ? null : (string[])PendingCandidateLabels.Clone(),
        DecisionCandidateLabels = DecisionCandidateLabels?.Select(labels =>
            (string[])labels.Clone()).ToArray()
    };

    public void Validate()
    {
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
    public PrototypeControlledEventRewardChoiceContinuationV111 Fork() => this with
    {
        PartialState = PartialState.Fork(),
        Decisions = Decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        DecisionCandidateLabels = DecisionCandidateLabels.Select(labels =>
            (string[])labels.Clone()).ToArray(),
        PendingRequest =
            PrototypeControlledEventChoiceContinuationV111.CloneRequest(
                PendingRequest),
        PendingCandidateLabels = (string[])PendingCandidateLabels.Clone()
    };

    public void Validate()
    {
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
    private readonly RandomIndexDecisionV111[] _decisions;
    private readonly Dictionary<string, int> _priorOrdinalCounts;
    private string[]? _candidateLabels;
    private int _nextDecision;
    private bool _disposed;

    public static bool IsActive => Active.Value is not null;

    private ControlledEventChoiceReplayV111(
        RandomIndexDecisionV111[] decisions,
        IReadOnlyDictionary<string, int>? priorOrdinalCounts)
    {
        _previous = Active.Value;
        _decisions = decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray();
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
            active._decisions
            .Take(active._nextDecision)
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
        if (active._nextDecision >= active._decisions.Length)
            throw new ControlledEventChoiceChanceRequiredException(
                normalizedRequest, candidateLabels);

        var decision = active._decisions[active._nextDecision++];
        decision.Validate();
        if (CanonicalJson.Sha256(decision.Request) !=
            CanonicalJson.Sha256(normalizedRequest))
            throw new InvalidDataException(
                "Event-choice replay diverged from its saved chance request.");
        return decision.SelectedIndex;
    }

    public void EnsureConsumed()
    {
        if (_nextDecision != _decisions.Length)
            throw new InvalidDataException(
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

    public static bool IsEventChoiceSite(string siteId) => siteId is
        "event.random_upgrade.card_instance" or
        "event.reward.random_rare_card.model" or
        "event.reward.random_potion.model" or
        "event.reward.offer_potion.model.index_draw" or
        "event.reward.card_bundle.shuffle_swap_index" or
        "relic_grab_bag.queue_shuffle.swap_index" or
        "relic_grab_bag.rarity.ticket" or
        "event.potion.fill_empty_slots.model" or
        "reward.card_rarity.ticket" or
        "reward.card_model.index_draw" or
        "run.random_pool_distinct.shuffle_swap_index" or
        "event.reward.random_modifier_curse.index_draw" or
        "event.fake_merchant.relic_offer.shuffle_swap_index" or
        "potion.uncommon_model.index_draw" or
        "relic.acquisition.random_rare_card.model" or
        "relic.acquisition.random_potion.model" or
        "run.potion.fill_empty_slots.model" or
        "reward.card_transform.replacement.index_draw" or
        "relic.unowned_random_selection.index_draw";

    public static bool IsRunActionSite(string siteId) => siteId is
        "run.encounter.overgrowth_boss.index_draw" or
        "map.legacy.floor_width.index_draw" or
        "map.legacy.edge_source.index_draw" or
        "map.legacy.orphan_target.index_draw" or
        "map.legacy.edge_extra_chance.ticket" or
        "map.legacy.second_edge_source.index_draw" or
        "map.legacy.convergence.order_key" or
        "map.legacy.convergence.source.index_draw" or
        "map.legacy.second_edge_target.index_draw" or
        "map.legacy.required_room.node.index_draw" or
        "map.legacy.room_assignment.index_draw";

    public static bool IsRunActionEventEntrySite(string siteId) => siteId is
        "event.selection.weighted_ticket" or
        "event.generated_gold.index_draw" or
        "event.stone_of_all_time.potion_slot.index_draw" or
        "event.fake_merchant.relic_offer.shuffle_swap_index" or
        "event.endless_conveyor.dish_weighted_ticket" or
        "event.future_of_potions.card_type.index_draw";

    public static bool IsControlledReplaySite(string siteId) =>
        IsEventChoiceSite(siteId) || IsRunActionSite(siteId)
        || IsRunActionEventEntrySite(siteId);
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
