using System.Globalization;

namespace Sts2Emulator.Core;

/// <summary>
/// Outcome transcript for a supported controlled stochastic run action.
/// Probability status remains unverified until source-law audit.
/// </summary>
public sealed record ControlledRunActionTraceV111(
    string ActionId,
    RandomIndexDecisionV111[] Decisions,
    string[][] CandidateLabels,
    string ProbabilityStatus = "UNVERIFIED",
    string? MapNodeId = null)
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
        var mapNodeAction = ActionId == "choose_map_node";
        if (ActionId is not ("start_run"
                or PrototypeNativeRestSiteActionsV111.Dig
                or "choose_map_node")
            || (mapNodeAction
                ? string.IsNullOrWhiteSpace(MapNodeId)
                : MapNodeId is not null)
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
            var isEventEntryDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionEventEntrySite(request.SiteId);
            var isShopDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionShopSite(request.SiteId);
            var isTreasureDraw = mapNodeAction
                && ControlledEventChoiceReplayV111
                    .IsRunActionTreasureSite(request.SiteId);
            var prefixStream = ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                || isEventEntryDraw
                ? "event" : isShopDraw ? "shop"
                : isTreasureDraw ? "reward"
                : isBossDraw ? "combat" : "map";
            var validSite = ActionId == "start_run"
                ? ControlledEventChoiceReplayV111.IsRunActionSite(
                    request.SiteId)
                : mapNodeAction
                    ? isEventEntryDraw || isShopDraw || isTreasureDraw
                    : ControlledEventChoiceReplayV111
                        .IsRunActionShovelSite(request.SiteId);
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
            var omittedLabels = (isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw)
                && request.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                && labels.Length == 0;
            var numericLabels = !(isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw)
                || omittedLabels || labels.SequenceEqual(
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
    string[] PendingCandidateLabels,
    string? MapNodeId = null)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the suspended run action. The
    /// chance payload and continuation fields remain flattened projections.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<
        PrototypeControlledRunActionMechanicsStateV111,
        ControlledChanceContinuationStateV111> Simulation { get; init; } = new(
            new PrototypeControlledRunActionMechanicsStateV111(
                PartialState, ActionId, MapNodeId),
            new ControlledChanceContinuationStateV111(
                Decisions, PendingRequest, PendingCandidateLabels,
                DecisionCandidateLabels));

    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeControlledRunActionMechanicsStateV111 Mechanics
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

    public string ActionId
    {
        get => Mechanics.ActionId;
        init => Mechanics = Mechanics with { ActionId = value };
    }

    public string? MapNodeId
    {
        get => Mechanics.MapNodeId;
        init => Mechanics = Mechanics with { MapNodeId = value };
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

    public PrototypeControlledRunActionContinuationV111 Fork() => this with
    {
        PartialState = PartialState.Fork(),
        Chance = Chance.Fork()
    };

    public void Validate()
    {
        this.Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ControlledRunAction);
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
        var mapNodeRequest = false;
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
                expectedSite = StochasticSiteRegistryV111
                    .UnownedRandomRelicSelectionSiteId;
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
            case "choose_map_node":
            {
                if (state.Phase != RunPhase.MapChoice
                    || state.World is not { } world
                    || state.NativeRunRng is not null
                    || string.IsNullOrWhiteSpace(MapNodeId))
                    throw new InvalidDataException(
                        "Controlled map-node frame is outside the supported legacy boundary.");
                var node = PrototypeNativeWingedBoots.AvailableNodes(
                        world, state.Player)
                    .SingleOrDefault(candidate => candidate.NodeId == MapNodeId);
                var eventEntrySite =
                    ControlledEventChoiceReplayV111.IsRunActionEventEntrySite(
                        PendingRequest.SiteId);
                var shopSite = ControlledEventChoiceReplayV111
                    .IsRunActionShopSite(PendingRequest.SiteId);
                var treasureSite = ControlledEventChoiceReplayV111
                    .IsRunActionTreasureSite(PendingRequest.SiteId);
                var legacyShopNode = node?.RoomType == PrototypeRoomType.Shop
                    && !PrototypeGameEngine.UsesNativeRegionalSystems(world);
                var availableRelics = node?.RoomType == PrototypeRoomType.Treasure
                    ? PrototypeContent.RelicPool
                        .Where(id => !state.Player.Relics.Any(relic =>
                            StringComparer.Ordinal.Equals(relic.RelicId, id)))
                        .ToArray()
                    : Array.Empty<string>();
                var legacyTreasureNode =
                    node?.RoomType == PrototypeRoomType.Treasure
                    && !PrototypeGameEngine.UsesNativeRegionalSystems(world)
                    && !PrototypeGameEngine.WillSkipNextTreasure(state.Player)
                    && availableRelics.Length > 0;
                if (node is null
                    || (node.RoomType != PrototypeRoomType.Event
                        && !legacyShopNode && !legacyTreasureNode))
                    throw new InvalidDataException(
                        "Controlled map-node frame does not reference an available supported node.");
                if (!(node.RoomType == PrototypeRoomType.Event && eventEntrySite)
                    && !(legacyShopNode && shopSite)
                    && !(legacyTreasureNode && treasureSite))
                    throw new InvalidDataException(
                        "Controlled map-node frame has an unsupported chance site.");
                expectedSite = PendingRequest.SiteId;
                expectedStream = shopSite ? "shop"
                    : treasureSite ? "reward" : "event";
                expectedOwner = PendingRequest.OwnerId;
                expectedCandidates = PendingRequest.OperationKind ==
                        "bounded_integer"
                    ? []
                    : (int[])PendingRequest.OrderedCandidates.Clone();
                expectedLabels = PendingRequest.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                    ? []
                    : Enumerable.Range(0, PendingRequest.SupportCount)
                        .Select(index => index.ToString(
                            CultureInfo.InvariantCulture)).ToArray();
                if (treasureSite)
                {
                    expectedSite = "treasure.relic_model.index_draw";
                    expectedStream = "reward";
                    expectedOwner = $"act:{world.Act}:floor:{world.Floor}:pool:"
                        + CanonicalJson.Sha256(availableRelics);
                    expectedCandidates = Enumerable.Range(
                        0, availableRelics.Length).ToArray();
                    expectedLabels = availableRelics.Length >
                        ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                        ? []
                        : Enumerable.Range(0, availableRelics.Length)
                            .Select(index => index.ToString(
                                CultureInfo.InvariantCulture)).ToArray();
                }
                mapNodeRequest = true;
                break;
            }
            default:
                throw new InvalidDataException(
                    "Controlled run-action frame uses an unsupported action.");
        }
        if ((!mapNodeRequest && MapNodeId is not null)
            || PendingRequest.SiteId != expectedSite
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
            var isEventEntryDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionEventEntrySite(request.SiteId);
            var isShopDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionShopSite(request.SiteId);
            var isTreasureDraw = ActionId == "choose_map_node"
                && ControlledEventChoiceReplayV111
                    .IsRunActionTreasureSite(request.SiteId);
            var prefixStream = ActionId ==
                    PrototypeNativeRestSiteActionsV111.Dig
                || isEventEntryDraw
                ? "event" : isShopDraw ? "shop"
                : isTreasureDraw ? "reward"
                : isBossDraw ? "combat" : "map";
            var validSite = ActionId == "start_run"
                ? ControlledEventChoiceReplayV111.IsRunActionSite(
                    request.SiteId)
                : ActionId == "choose_map_node"
                    ? isEventEntryDraw || isShopDraw || isTreasureDraw
                    : ControlledEventChoiceReplayV111
                        .IsRunActionShovelSite(request.SiteId);
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
            var omittedLabels = (isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw)
                && request.SupportCount >
                    ControlledRunActionEngineV111.MaximumInlineOutcomeActions
                && labels.Length == 0;
            var numericLabels = !(isMapDraw || isEventEntryDraw || isShopDraw
                || isTreasureDraw)
                || omittedLabels || labels.SequenceEqual(
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
                    "Controlled run-action decision prefix is malformed.");
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
        var mapNodeId = action.Kind == "choose_map_node"
            ? action.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
        var supportedMapNodeAction = source.Phase == RunPhase.MapChoice
            && source.NativeRunRng is null
            && source.World is { } mapNodeWorld
            && PrototypeNativeWingedBoots.AvailableNodes(
                    mapNodeWorld, source.Player)
                .Any(node => node.NodeId == mapNodeId
                    && (node.RoomType == PrototypeRoomType.Event
                        || node.RoomType == PrototypeRoomType.Shop
                            && !PrototypeGameEngine
                                .UsesNativeRegionalSystems(mapNodeWorld)
                        || node.RoomType == PrototypeRoomType.Treasure
                            && !PrototypeGameEngine
                                .UsesNativeRegionalSystems(mapNodeWorld)
                            && !PrototypeGameEngine
                                .WillSkipNextTreasure(source.Player)
                            && PrototypeContent.RelicPool.Any(id =>
                                !source.Player.Relics.Any(relic =>
                                    StringComparer.Ordinal.Equals(
                                        relic.RelicId, id)))));
        if (source.ControlledRunActionChoice is not null
            || action.Kind is not ("start_run"
                or PrototypeNativeRestSiteActionsV111.Dig
                or "choose_map_node")
            || action.Kind == "choose_map_node" && !supportedMapNodeAction)
            throw new InvalidOperationException(
                "Controlled run action requires an unsuspended supported action.");
        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(source).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "Controlled run action requires a current legal action.");
        RunState result;
        using (ControlledChanceGuardV111.Enter())
        using (var replay = ControlledEventChoiceReplayV111.Begin([]))
        {
            try
            {
                result = engine.Step(source, action).State;
                replay.EnsureConsumed();
            }
            catch (ControlledEventChoiceChanceRequiredException exception)
            {
                if (!supportedMapNodeAction
                    || !(ControlledEventChoiceReplayV111
                            .IsRunActionEventEntrySite(exception.Request.SiteId)
                        || ControlledEventChoiceReplayV111
                            .IsRunActionShopSite(exception.Request.SiteId)
                        || ControlledEventChoiceReplayV111
                            .IsRunActionTreasureSite(exception.Request.SiteId))
                    || exception.CandidateLabels is null
                    || (exception.CandidateLabels.Length !=
                            exception.Request.SupportCount
                        && !(exception.CandidateLabels.Length == 0
                            && exception.Request.SupportCount >
                                MaximumInlineOutcomeActions)))
                    throw new NotSupportedException(
                        "Controlled run action reached an unsupported chance request.");

                var partialState = source.Fork() with
                {
                    DecisionIndex = checked(source.DecisionIndex + 1)
                };
                var continuation =
                    new PrototypeControlledRunActionContinuationV111(
                        partialState,
                        "choose_map_node",
                        [],
                        [],
                        exception.Request,
                        exception.CandidateLabels,
                        mapNodeId);
                continuation.Validate();
                var suspended = partialState with
                {
                    ControlledRunActionChoice = continuation
                };
                PrototypeStateInvariants.Validate(suspended);
                return suspended;
            }
        }
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
