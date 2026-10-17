using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Outcome transcript for a controlled stochastic run action that is not an
/// event choice. Probability status remains unverified until source-law audit.
/// </summary>
public sealed record ControlledRunActionTraceV111(
    string ActionId,
    RandomIndexDecisionV111[] Decisions,
    string[][] CandidateLabels,
    string ProbabilityStatus = "UNVERIFIED")
{
    public ControlledRunActionTraceV111 Fork() => this with
    {
        Decisions = Decisions.Select(
            PrototypeControlledEventChoiceContinuationV111.CloneDecision)
            .ToArray(),
        CandidateLabels = CandidateLabels.Select(labels =>
            (string[])labels.Clone()).ToArray()
    };

    public void Validate()
    {
        if (ActionId is not ("start_run"
                or PrototypeNativeRestSiteActionsV111.Dig)
            || Decisions is null || CandidateLabels is null
            || Decisions.Length == 0
            || Decisions.Length != CandidateLabels.Length
            || ProbabilityStatus != "UNVERIFIED")
            throw new InvalidDataException(
                "Malformed controlled run-action transcript.");
        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            var labels = CandidateLabels[index];
            if (labels is null)
                throw new InvalidDataException(
                    "Controlled run-action transcript is missing candidate labels.");
            decision.Validate();
            var request = decision.Request;
            var isBossDraw = request.SiteId ==
                "run.encounter.overgrowth_boss.index_draw";
            var isMapDraw = request.SiteId.StartsWith(
                "map.legacy.", StringComparison.Ordinal);
            var prefixStream = ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                ? "event" : isBossDraw ? "combat" : "map";
            var validSite = ActionId == "start_run"
                ? ControlledEventChoiceReplayV111.IsRunActionSite(
                    request.SiteId)
                : request.SiteId ==
                    "relic.unowned_random_selection.index_draw";
            var prefixOwner = isBossDraw
                ? "run:act:1:boss_pool:" + CanonicalJson.Sha256(labels)
                : ActionId == PrototypeNativeRestSiteActionsV111.Dig
                    ? "rest:shovel:pool:" + CanonicalJson.Sha256(labels)
                    : request.OwnerId;
            var prefixCandidates = request.OperationKind == "bounded_integer"
                ? Array.Empty<int>()
                : isBossDraw || ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                    ? Enumerable.Range(0, labels.Length).ToArray()
                    : (int[])request.OrderedCandidates.Clone();
            var omittedLabels = isMapDraw
                && request.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                && labels.Length == 0;
            var numericLabels = !isMapDraw || omittedLabels || labels.SequenceEqual(
                Enumerable.Range(0, labels.Length).Select(index =>
                    index.ToString(CultureInfo.InvariantCulture)),
                StringComparer.Ordinal);
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !validSite
                || request.StreamId != prefixStream
                || request.OwnerId != prefixOwner
                || !request.OrderedCandidates.SequenceEqual(prefixCandidates)
                || (labels.Length != request.SupportCount && !omittedLabels)
                || !numericLabels
                || labels.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled run-action transcript contains an invalid outcome.");
        }
    }
}

/// <summary>
/// Serializable checkpoint for a supported run action replayed from its
/// pre-action state until the next unresolved controlled outcome.
/// </summary>
public sealed record PrototypeControlledRunActionContinuationV111(
    RunState PartialState,
    string ActionId,
    RandomIndexDecisionV111[] Decisions,
    string[][] DecisionCandidateLabels,
    RandomIndexRequestV111 PendingRequest,
    string[] PendingCandidateLabels)
{
    public PrototypeControlledRunActionContinuationV111 Fork() => this with
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
        if (PartialState is null || ActionId is null
            || Decisions is null || DecisionCandidateLabels is null
            || DecisionCandidateLabels.Length != Decisions.Length
            || PendingRequest is null || PendingCandidateLabels is null)
            throw new InvalidDataException(
                "Malformed controlled run-action continuation.");

        var state = PartialState;
        if (state.ControlledRunActionChoice is not null
            || state.ControlledEventRewardChoice is not null
            || state.World?.Event?.ControlledChoice is not null)
            throw new InvalidDataException(
                "Controlled run-action frame contains another pending controller.");

        PendingRequest.Validate();
        string expectedSite;
        string expectedStream;
        string expectedOwner;
        int[] expectedCandidates;
        string[] expectedLabels;
        switch (ActionId)
        {
            case "start_run":
            {
                if (state.Phase != RunPhase.RunStart
                    || state.World is not null
                    || state.Configuration is not null)
                    throw new InvalidDataException(
                        "Controlled run-start frame is outside the legacy run-start boundary.");
                if (PendingRequest.SiteId ==
                    "run.encounter.overgrowth_boss.index_draw")
                {
                    expectedSite = "run.encounter.overgrowth_boss.index_draw";
                    expectedStream = "combat";
                    var bossPool = PrototypeContent.OvergrowthBossEncounterPool;
                    expectedOwner = "run:act:1:boss_pool:"
                        + CanonicalJson.Sha256(bossPool);
                    expectedCandidates = Enumerable.Range(
                        0, bossPool.Length).ToArray();
                    expectedLabels = (string[])bossPool.Clone();
                }
                else if (ControlledEventChoiceReplayV111.IsRunActionSite(
                    PendingRequest.SiteId)
                    && PendingRequest.SiteId.StartsWith(
                        "map.legacy.", StringComparison.Ordinal))
                {
                    expectedSite = PendingRequest.SiteId;
                    var mapStream = "map";
                    var mapOwner = PendingRequest.OwnerId;
                    expectedLabels = PendingRequest.SupportCount >
                        ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                        ? []
                        : Enumerable.Range(
                            0, PendingRequest.SupportCount).Select(index =>
                                index.ToString(CultureInfo.InvariantCulture)).ToArray();
                    var mapCandidates = PendingRequest.OperationKind
                        == "bounded_integer"
                        ? []
                        : (int[])PendingRequest.OrderedCandidates.Clone();
                    expectedStream = mapStream;
                    expectedOwner = mapOwner;
                    expectedCandidates = mapCandidates;
                }
                else
                {
                    throw new InvalidDataException(
                        "Controlled run-start frame has an unsupported chance site.");
                }
                break;
            }
            case PrototypeNativeRestSiteActionsV111.Dig:
            {
                if (state.Phase != RunPhase.Rest
                    || !PrototypeNativeRestSiteRelics.CanDig(state.Player))
                    throw new InvalidDataException(
                        "Controlled Shovel frame is outside the legacy rest-site boundary.");
                expectedSite = "relic.unowned_random_selection.index_draw";
                expectedStream = "event";
                expectedLabels = PrototypeContent.RelicPool
                    .Where(id => !state.Player.Relics.Any(relic =>
                        StringComparer.Ordinal.Equals(relic.RelicId, id)))
                    .ToArray();
                expectedOwner = "rest:shovel:pool:"
                    + CanonicalJson.Sha256(expectedLabels);
                expectedCandidates = Enumerable.Range(
                    0, expectedLabels.Length).ToArray();
                break;
            }
            default:
                throw new InvalidDataException(
                    "Controlled run-action frame uses an unsupported action.");
        }
        if (PendingRequest.SiteId != expectedSite
            || PendingRequest.StreamId != expectedStream
            || PendingRequest.OwnerId != expectedOwner
            || !PendingRequest.OrderedCandidates.SequenceEqual(expectedCandidates)
            || PendingCandidateLabels.Length != expectedLabels.Length
            || !PendingCandidateLabels.SequenceEqual(expectedLabels,
                StringComparer.Ordinal))
            throw new InvalidDataException(
                "Controlled run-action candidates do not match their source pool.");

        for (var index = 0; index < Decisions.Length; index++)
        {
            var decision = Decisions[index];
            var labels = DecisionCandidateLabels[index];
            if (labels is null)
                throw new InvalidDataException(
                    "Controlled run-action decision prefix is missing candidate labels.");
            decision.Validate();
            var request = decision.Request;
            var isBossDraw = request.SiteId ==
                "run.encounter.overgrowth_boss.index_draw";
            var isMapDraw = request.SiteId.StartsWith(
                "map.legacy.", StringComparison.Ordinal);
            var prefixStream = ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                ? "event" : isBossDraw ? "combat" : "map";
            var validSite = ActionId == "start_run"
                ? ControlledEventChoiceReplayV111.IsRunActionSite(
                    request.SiteId)
                : request.SiteId ==
                    "relic.unowned_random_selection.index_draw";
            var prefixOwner = isBossDraw
                ? "run:act:1:boss_pool:" + CanonicalJson.Sha256(labels)
                : ActionId == PrototypeNativeRestSiteActionsV111.Dig
                    ? "rest:shovel:pool:" + CanonicalJson.Sha256(labels)
                    : request.OwnerId;
            var prefixCandidates = request.OperationKind == "bounded_integer"
                ? Array.Empty<int>()
                : isBossDraw || ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                    ? Enumerable.Range(0, labels.Length).ToArray()
                    : (int[])request.OrderedCandidates.Clone();
            var omittedLabels = isMapDraw
                && request.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                && labels.Length == 0;
            var numericLabels = !isMapDraw || omittedLabels || labels.SequenceEqual(
                Enumerable.Range(0, labels.Length).Select(index =>
                    index.ToString(CultureInfo.InvariantCulture)),
                StringComparer.Ordinal);
            if (decision.ProviderMode != "controlled-v111"
                || decision.NativeCursorBefore is not null
                || decision.NativeCursorAfter is not null
                || !validSite
                || request.StreamId != prefixStream
                || request.OwnerId != prefixOwner
                || !request.OrderedCandidates.SequenceEqual(prefixCandidates)
                || (request.SupportCount != labels.Length && !omittedLabels)
                || !numericLabels
                || labels.Any(string.IsNullOrWhiteSpace))
                throw new InvalidDataException(
                    "Controlled Shovel decision prefix is malformed.");
        }

        PrototypeStateInvariants.Validate(state);
    }
}

/// <summary>
/// Opt-in controlled boundary for supported stochastic run actions. Ordinary
/// player steps retain their existing provider behavior.
/// </summary>
public static class ControlledRunActionEngineV111
{
    public const string SchemaId = "sts2-v111-controlled-run-action-v1";
    public const int MaximumInlineOutcomeActions = 4096;

    public static RunState BeginLegacyRunStart(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Phase != RunPhase.RunStart
            || source.Configuration is not null)
            throw new InvalidOperationException(
                "Controlled run start requires an unsuspended legacy run-start state.");
        return Begin(source, GameAction.Empty("start_run"));
    }

    public static RunState BeginShovelDig(RunState source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Phase != RunPhase.Rest
            || source.ControlledRunActionChoice is not null
            || !PrototypeNativeRestSiteRelics.CanDig(source.Player))
            throw new InvalidOperationException(
                "Controlled Shovel dig requires an unsuspended rest-site action.");

        var action = new PrototypeGameEngine().GetLegalActions(source)
            .SingleOrDefault(candidate =>
                candidate.Kind == PrototypeNativeRestSiteActionsV111.Dig)
            ?? throw new InvalidOperationException(
                "Shovel dig is not legal in the current state.");
        return Begin(source, action);
    }

    public static RunState Begin(RunState source, GameAction action)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        PrototypeStateInvariants.Validate(source);
        if (source.ControlledRunActionChoice is not null
            || action.Kind is not ("start_run"
                or PrototypeNativeRestSiteActionsV111.Dig))
            throw new InvalidOperationException(
                "Controlled run action requires an unsuspended supported action.");
        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(source).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "Controlled run action requires a current legal action.");
        using var guard = ControlledChanceGuardV111.Enter();
        using var replay = ControlledEventChoiceReplayV111.Begin([]);
        var result = engine.Step(source, action).State;
        replay.EnsureConsumed();
        if (result.ControlledRunActionChoice is null)
            throw new NotSupportedException(
                "The selected action did not reach a supported legacy controlled chance site.");
        PrototypeStateInvariants.Validate(result);
        return result;
    }

    public static RunState Resolve(RunState source, int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.ControlledRunActionChoice is null)
            throw new InvalidOperationException(
                "No controlled run-action outcome is pending.");
        return Step(source, GameAction.Create(
            "choose_controlled_run_action_outcome",
            new ChooseControlledEventOutcomePayload(selectedIndex)));
    }

    private static RunState Step(RunState source, GameAction action)
    {
        using var guard = ControlledChanceGuardV111.Enter();
        var result = new PrototypeGameEngine().Step(source, action).State;
        PrototypeStateInvariants.Validate(result);
        return result;
    }
}
