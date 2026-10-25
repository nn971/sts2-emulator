using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureRunActionSessionSnapshotV111(
    string SchemaId, string OriginSnapshotJson, GameAction Action,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public bool LegacyNativeAncientOfferCompletion { get; init; }

    private ControlledStochasticDecisionV111[]?
        _nativeCombatEntryProviderDecisions;

    public ControlledStochasticDecisionV111[]?
        NativeCombatEntryProviderDecisions
    {
        get => CloneProviderDecisions(_nativeCombatEntryProviderDecisions);
        init => _nativeCombatEntryProviderDecisions =
            CloneProviderDecisions(value);
    }

    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        Copy(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Copy(_decisionTranscript)!;
        init => _decisionTranscript = Copy(value);
    }

    private static ReferenceNatureDecisionV111[]? Copy(
        ReferenceNatureDecisionV111[]? transcript) => transcript is null
        ? null
        : (ReferenceNatureDecisionV111[])transcript.Clone();

    private static ControlledStochasticDecisionV111[]?
        CloneProviderDecisions(
            ControlledStochasticDecisionV111[]? decisions) => decisions?
                .Select(decision => decision?.Fork()!).ToArray();
}

/// <summary>
/// Atomic ReferenceNature envelope for existing controlled run-action
/// continuations. It reuses the run mechanics resolver and the same sampler
/// state as semantic combat sessions.
/// </summary>
public sealed record ReferenceNatureRunActionSessionV111(
    string SchemaId, RunState Origin, GameAction Action, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the saved run action. The
    /// session's existing flattened properties remain its snapshot contract.
    /// </summary>
    [JsonIgnore]
    public SimulationStateV111<ReferenceNatureRunActionMechanicsStateV111,
        ReferenceNatureRunActionStochasticStateV111> Simulation { get; init; } =
        new(
            new ReferenceNatureRunActionMechanicsStateV111(
                Origin, Action, Run),
            new ReferenceNatureRunActionStochasticStateV111(
                InitialNatureState, NatureState, DecisionTranscript));

    [JsonIgnore]
    public ReferenceNatureRunActionMechanicsStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureRunActionStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public GameAction Action
    {
        get => Mechanics.Action;
        init => Mechanics = Mechanics with { Action = value };
    }

    public RunState Run
    {
        get => Mechanics.Run;
        init => Mechanics = Mechanics with { Run = value };
    }

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Stochastic.InitialNatureState;
        init => Stochastic = Stochastic with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => Stochastic.DecisionTranscript;
        init => Stochastic = Stochastic with { DecisionTranscript = value };
    }

    public ReferenceNatureMapGenerationSessionV111? MapGeneration
    { get; init; }

    public ReferenceNatureLaterActPreparationSessionV111?
        LaterActPreparation { get; init; }

    public ReferenceNatureAncientOfferSessionV111? AncientOffer
    { get; init; }

    public ControlledStochasticContinuationStateV111?
        NativeCombatEntryContinuation
    {
        get => Stochastic.NativeCombatEntryContinuation;
        init => Stochastic = Stochastic with
        {
            NativeCombatEntryContinuation = value
        };
    }

    public ControlledStochasticDecisionV111[] NativeCombatEntryProviderDecisions
    {
        get => Stochastic.NativeCombatEntryProviderDecisions;
        init => Stochastic = Stochastic with
        {
            NativeCombatEntryProviderDecisions = value
        };
    }

    public bool LegacyNativeAncientOfferCompletion { get; init; }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-run-action-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool Complete => Run.Stochastic.ControlledRunActionChoice is null
        && MapGeneration is null && LaterActPreparation is null
        && AncientOffer is null && NativeCombatEntryContinuation is null;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw
    {
        get
        {
            if (LaterActPreparation is { } laterActPreparation)
                return laterActPreparation.PendingLaw;
            if (AncientOffer is { } ancientOffer)
                return ancientOffer.PendingLaw;
            if (MapGeneration is { Complete: false } mapGeneration)
                return mapGeneration.PendingLaw;
            if (NativeCombatEntryContinuation is { } nativeCombatEntry)
                return QueryNativeRunActionLaw(
                    Origin, Action, nativeCombatEntry, DecisionTranscript);
            if (Run.Stochastic.ControlledRunActionChoice is null)
                return null;
            return ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureOvergrowthRunStartSource(
                    Origin)
                && Action.Kind == "start_run"
                ? ChanceLawEngineV111.QueryOvergrowthBossEncounter(0)
                : ChanceLawEngineV111.QueryControlledRunAction(Run);
        }
    }

    public static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action, ulong seed) =>
        Begin(source, action,
            ReferenceNatureRunCursorV111.Begin(
                source, seed, "Run-action"));

    public static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action) =>
        Begin(source, action,
            ReferenceNatureRunCursorV111.Begin(
                source, null, "Run-action"));

    private static ReferenceNatureRunActionSessionV111 Begin(
        RunState source, GameAction action,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Run-action");
        var detachedAction = CloneAction(action);
        if (IsConfiguredExperimentalActTransitionAction(
                source, detachedAction))
        {
            var actionId = PrototypeAiEnvironment.StableActionId(
                detachedAction);
            if (!new PrototypeGameEngine().GetLegalActions(source).Any(
                    candidate => StringComparer.Ordinal.Equals(
                        PrototypeAiEnvironment.StableActionId(candidate),
                        actionId)))
                throw new InvalidOperationException(
                    "ReferenceNature act-transition session requires a current legal action.");
            var nextAct = checked(source.World!.Act + 1);
            var mapGeneration = ReferenceNatureMapGenerationSessionV111.Begin(
                source.RunSeed, nextAct, source.Ascension,
                initialNatureState);
            var transitionSession = new ReferenceNatureRunActionSessionV111(
                CurrentSchemaId, source.Fork(), detachedAction,
                source.Fork(), initialNatureState.Fork(),
                initialNatureState.Fork(), [])
            {
                MapGeneration = mapGeneration
            };
            if (mapGeneration.Complete)
                transitionSession = transitionSession
                    .AdvanceAfterExperimentalActTransitionMap(
                        mapGeneration);
            transitionSession.Validate();
            return transitionSession;
        }
        var configuredNativeRunStart =
            ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureRunStartAction(
                    source, detachedAction);
        var natureRelicBagPull = ControlledRunActionEngineV111
                .IsLegacyNativeTreasureEntryAction(source, detachedAction)
            || ControlledRunActionEngineV111
                .IsLegacyNativeShovelDigAction(source, detachedAction);
        var origin = natureRelicBagPull
            ? ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, createRelicBagWhenMissing: true)
            : source.Fork();
        if (UsesTypedRunAction(origin, detachedAction))
        {
            var actionId = PrototypeAiEnvironment.StableActionId(
                detachedAction);
            if (!new PrototypeGameEngine().GetLegalActions(source).Any(
                    candidate => StringComparer.Ordinal.Equals(
                        PrototypeAiEnvironment.StableActionId(candidate),
                        actionId)))
                throw new InvalidOperationException(
                    "ReferenceNature Native typed run-action session requires a current legal action.");
            var replay = CaptureNativeRunAction(
                origin, detachedAction, []);
            var providerDecisions = replay.Continuation?.Decisions ?? [];
            var typedRun = replay.Run is { } completed
                ? CommitNativeRunActionTrace(
                    ReferenceNatureRunCursorV111.Commit(
                        completed, initialNatureState), detachedAction,
                    providerDecisions)
                : origin.Fork();
            var typedSession = new ReferenceNatureRunActionSessionV111(
                CurrentSchemaId, origin, detachedAction, typedRun,
                initialNatureState.Fork(), initialNatureState.Fork(), [])
            {
                NativeCombatEntryContinuation =
                    replay.Continuation?.Fork(),
                NativeCombatEntryProviderDecisions = providerDecisions
            };
            typedSession.Validate();
            return typedSession;
        }
        RunState run;
        using (configuredNativeRunStart
                   ? V111RunFactory.EnterControlledReferenceNatureRunStart()
                   : null)
            run = ControlledRunActionEngineV111.Begin(
                origin.Fork(), detachedAction);
        var session = new ReferenceNatureRunActionSessionV111(
            CurrentSchemaId, origin, detachedAction, run,
            initialNatureState.Fork(), initialNatureState.Fork(), []);
        if (session.Complete)
            session = session with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    session.Run, initialNatureState)
            };
        session.Validate();
        return session;
    }

    private static bool IsConfiguredExperimentalActTransitionAction(
        RunState source, GameAction action) =>
        action.Kind == "continue_act"
        && source.Configuration?.UsesExperimentalSilentThreeActRoute == true
        && source.Configuration.CharacterId == "silent"
        && source.Phase == RunPhase.ActTransition
        && source.World is { Act: >= 1 and < 3 };

    public ReferenceNatureRunActionSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork(),
            MapGeneration = MapGeneration?.Fork(),
            LaterActPreparation = LaterActPreparation?.Fork(),
            AncientOffer = AncientOffer?.Fork()
        };
    }

    public ReferenceNatureRunActionSessionV111 ResolveNext()
    {
        Validate();
        if (LaterActPreparation is not null)
            return ResolveNextLaterActPreparationOutcome();
        if (AncientOffer is not null)
            return ResolveNextAncientOfferOutcome();
        if (MapGeneration is not null)
            return ResolveNextMapGenerationOutcome();
        if (NativeCombatEntryContinuation is not null)
            return ResolveNextNativeRunActionOutcome();
        var law = PendingLaw ?? throw new InvalidOperationException(
            "No ReferenceNature run-action choice is pending.");
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var configuredNativeRunStart =
            ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureRunStartAction(
                    Origin, Action);
        var configuredExperimentalRunStart = configuredNativeRunStart
            && Origin.Configuration?.UsesExperimentalSilentThreeActRoute == true;
        RunState resolvedRun;
        using (configuredNativeRunStart
                   ? V111RunFactory.EnterControlledReferenceNatureRunStart()
                   : null)
            resolvedRun = ControlledRunActionEngineV111.Resolve(
                Run.Fork(), selection.OutcomeIndex);
        var decision = new ReferenceNatureDecisionV111(
            law.LawId, law.EventId, selection.OutcomeId,
            selection.OutcomeIndex);
        if (resolvedRun.Stochastic.ControlledRunActionChoice is null)
        {
            if (ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureOvergrowthRunStartSource(
                    Origin) && Action.Kind == "start_run")
            {
                var mapGeneration = ReferenceNatureMapGenerationSessionV111
                    .Begin(Origin.RunSeed, 1, Origin.Ascension,
                        selection.NextState);
                var overgrowthNext = this with
                {
                    Run = resolvedRun,
                    Stochastic = Stochastic.Advance(
                        selection.NextState, decision),
                    MapGeneration = mapGeneration
                };
                if (mapGeneration.Complete)
                    overgrowthNext =
                        overgrowthNext.ContinueAfterOvergrowthRunStartMap(
                        mapGeneration);
                overgrowthNext.Validate();
                return overgrowthNext;
            }
            if (configuredExperimentalRunStart)
            {
                var profile = Origin.Configuration!.Profile;
                var laterActPreparation =
                    ReferenceNatureLaterActPreparationSessionV111.Begin(
                        resolvedRun.Stochastic.PrototypeRng, Origin.Ascension, profile,
                        selection.NextState);
                var experimentalNext = this with
                {
                    Run = resolvedRun,
                    Stochastic = Stochastic.Advance(
                        selection.NextState, decision),
                    LaterActPreparation = laterActPreparation
                };
                if (laterActPreparation.Complete)
                    experimentalNext =
                        experimentalNext.CommitExperimentalThreeActRunStart();
                experimentalNext.Validate();
                return experimentalNext;
            }
            if (configuredNativeRunStart)
            {
                var initializationTranscript =
                    new ReferenceNatureRunInitializationTranscriptV111(
                        InitialNatureState, selection.NextState,
                        [law], [decision]);
                initializationTranscript.Validate();
                resolvedRun = resolvedRun with
                {
                    Stochastic = RunStochasticStateV111
                        .CommitReferenceNatureRunInitialization(
                            resolvedRun.Stochastic, selection.NextState,
                            initializationTranscript, pending: false)
                };
            }
            else
            {
                resolvedRun = ReferenceNatureRunCursorV111.Commit(
                    resolvedRun, selection.NextState);
            }
        }
        var next = this with
        {
            Run = resolvedRun,
            Stochastic = Stochastic.Advance(selection.NextState, decision)
        };
        next.Validate();
        return next;
    }

    private ReferenceNatureRunActionSessionV111
        ResolveNextNativeRunActionOutcome()
    {
        var continuation = NativeCombatEntryContinuation ??
            throw new InvalidOperationException(
                "No Native typed run-action stochastic request is pending.");
        var law = PendingLaw ?? throw new InvalidDataException(
            "Native typed run-action continuation has no Chance Law.");
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var providerDecision = ReferenceNatureChanceLawDecisionV111
            .FromSelection(
                law, continuation.PendingRequest, selection, Origin,
                continuation.PendingCandidateLabels);
        var replay = ControlledChanceContinuationExecutorV111
            .ResolveStochastic(
                Origin, continuation, providerDecision,
                checkpoint => new PrototypeGameEngine().Step(
                    checkpoint, CloneAction(Action)).State,
                request => IsTypedRunActionRequest(
                    Origin, Action, request));
        var providerDecisions = replay.Continuation?.Decisions ??
            continuation.Decisions.Append(providerDecision.Fork()).ToArray();
        var run = replay.Run is { } completed
            ? CommitNativeRunActionTrace(
                ReferenceNatureRunCursorV111.Commit(
                    completed, selection.NextState), Action,
                providerDecisions)
            : Origin.Fork();
        var decision = new ReferenceNatureDecisionV111(
            law.LawId, law.EventId, selection.OutcomeId,
            selection.OutcomeIndex);
        var next = this with
        {
            Run = run,
            Stochastic = Stochastic.Advance(
                selection.NextState, decision),
            NativeCombatEntryContinuation = replay.Continuation?.Fork(),
            NativeCombatEntryProviderDecisions = providerDecisions
        };
        next.Validate();
        return next;
    }

    private static bool UsesTypedRunAction(
        RunState source, GameAction action) =>
        UsesTypedNativePotionFactoryCombatEntry(source, action)
        || ControlledRunActionEngineV111.IsNativeRestHealRewardAction(
            source, action)
        || IsEntropicBrewAction(source, action);

    private static bool IsEntropicBrewAction(
        RunState source, GameAction action) =>
        ControlledRunActionEngineV111.IsEntropicBrewRunAction(
            source, action);

    private static bool IsEntropicBrewFillRequest(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request)
    {
        return request.IndexRequest is { } indexRequest
            && ControlledRunActionEngineV111.IsEntropicBrewRunAction(
                source, action)
            && ControlledRunActionEngineV111.IsEntropicBrewFillRequest(
                source, action, indexRequest);
    }

    private static bool UsesTypedNativePotionFactoryCombatEntry(
        RunState source, GameAction action) =>
        ControlledRunActionEngineV111.IsNativeActOneCombatEntryAction(
            source, action)
        && source.Player.PotionSlots.Any(slot => slot is null)
        && source.Player.ActiveRelics.Any(relic =>
            PrototypeContent.Relic(relic.RelicId)
                .ProcureRandomPotionsBeforeCombat);

    private static ControlledStochasticContinuationResultV111
        CaptureNativeRunAction(
            RunState source, GameAction action,
            ControlledStochasticDecisionV111[] decisions) =>
        ControlledChanceContinuationExecutorV111.CaptureStochastic(
            source,
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            decisions,
            checkpoint => new PrototypeGameEngine().Step(
                checkpoint, CloneAction(action)).State,
            request => IsTypedRunActionRequest(source, action, request));

    private static RunState CommitNativeRunActionTrace(
        RunState run, GameAction action,
        ControlledStochasticDecisionV111[] decisions)
    {
        if (decisions.Length == 0)
            return run;
        var indexDecisions = decisions.Where(decision =>
            decision.IndexDecision is not null).ToArray();
        var trace = new ControlledRunActionTraceV111(
            action.Kind,
            indexDecisions.Select(decision => decision.IndexDecision!)
                .ToArray(),
            indexDecisions.Select(decision => decision.CandidateLabels ?? [])
                .ToArray(),
            MapNodeId: action.Kind == "choose_map_node"
                ? action.ReadPayload<ChooseMapNodePayload>().NodeId : null,
            ActionFingerprint: action.Kind is
                PrototypeNativeRestSiteActionsV111.Heal or "use_potion"
                ? PrototypeAiEnvironment.StableActionId(action) : null)
        {
            DecisionTranscriptSchemaId =
                ControlledRunActionTraceV111
                    .MixedPrimitiveTranscriptSchemaId,
            TypedDecisions = decisions
        };
        return run with
        {
            Stochastic = run.Stochastic
                .CompleteControlledRunActionTrace(trace)
        };
    }

    private static bool IsTypedRunActionRequest(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request)
    {
        if (ControlledRunActionEngineV111
                .IsNativeRestHealRewardRequest(source, action, request))
            return true;
        if (IsEntropicBrewFillRequest(source, action, request))
            return true;
        if (!UsesTypedNativePotionFactoryCombatEntry(source, action))
            return false;
        if (ControlledRunActionEngineV111
                .IsNativeActOneCombatEntryRequest(request)
            && ControlledRunActionEngineV111.IsNativeActOneCombatEntrySite(
                source, action, request.SiteId))
            return true;
        return IsNativeCombatEntryPotionFactoryRequest(
            source, action, request);
    }

    private static bool IsNativeCombatEntryPotionFactoryRequest(
        RunState source, GameAction action,
        ControlledStochasticRequestV111 request)
    {
        var rarityRequest = request.UnitFloatRequest;
        var modelRequest = request.IndexRequest;
        var siteMatches = rarityRequest is not null
            ? rarityRequest.SiteId ==
                "potion_factory.rarity_roll.unit_float"
            : modelRequest?.SiteId ==
                "potion_factory.model_within_rarity.index_draw";
        if (!siteMatches || request.StreamId !=
                "run.combat_potion_generation" || request.Ordinal != 0)
            return false;
        return NativeCombatEntryPotionOwners(source, action).Contains(
            request.OwnerId, StringComparer.Ordinal);
    }

    private static string[] NativeCombatEntryPotionOwners(
        RunState source, GameAction action)
    {
        if (source.World is not { } world ||
            action.Kind != "choose_map_node")
            return [];
        var nodeId = action.ReadPayload<ChooseMapNodePayload>().NodeId;
        var node = world.Map.Nodes.SingleOrDefault(candidate =>
            candidate.NodeId == nodeId);
        if (node is null)
            return [];
        var owners = new List<string>();
        for (var slotIndex = 0;
            slotIndex < source.Player.PotionSlots.Length; slotIndex++)
            if (source.Player.PotionSlots[slotIndex] is null)
                owners.Add(
                    $"combat:act:{world.Act}:floor:{node.Floor}:node:{node.NodeId}:potion_slot:{slotIndex}");
        return owners.ToArray();
    }

    private static ChanceLawV111 QueryNativeRunActionLaw(
        RunState source, GameAction action,
        ControlledStochasticContinuationStateV111 continuation,
        ReferenceNatureDecisionV111[] natureDecisions)
    {
        continuation.Validate();
        var request = continuation.PendingRequest;
        if (ControlledRunActionEngineV111
                .IsNativeRestHealRewardAction(source, action))
            return QueryNativeRestHealRewardLaw(
                source, action, continuation, natureDecisions);
        if (request.IndexRequest is { } potionFillRequest &&
            IsEntropicBrewFillRequest(
                source, action, request))
        {
            if (potionFillRequest.StreamId ==
                NativePlayerRngV111.RewardsStreamId)
                return ChanceLawEngineV111
                    .QueryNativeRunPotionFillSlotModel(potionFillRequest);
            var slotPrefix = "player:potion_slot:";
            var poolSuffix = ":pool:" + CanonicalJson.Sha256(
                PrototypeContent.PotionPool);
            var slotText = potionFillRequest.OwnerId[
                slotPrefix.Length..^poolSuffix.Length];
            if (!int.TryParse(slotText, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var slotIndex))
                throw new InvalidDataException(
                    "Legacy Entropic Brew slot owner is malformed.");
            return ChanceLawEngineV111.QueryLegacyRunPotionFillSlotModel(
                slotIndex, canAcquirePotion: true, slotIsEmpty: true,
                ordinal: potionFillRequest.Ordinal);
        }
        if (!IsTypedRunActionRequest(source, action, request))
            throw new NotSupportedException(
                $"ReferenceNature Native run action does not support stochastic site '{request.SiteId}'.");

        if (request.IndexRequest is { } indexRequest &&
            ControlledRunActionEngineV111.IsNativeEncounterFormationRequest(
                request))
        {
            if (!indexRequest.OrderedCandidates.SequenceEqual(
                    Enumerable.Range(0, indexRequest.SupportCount)))
                throw new InvalidDataException(
                    "Native encounter formation support is not a source-ordered index range.");
            var owner = indexRequest.OwnerId;
            var floorSeparator = owner.LastIndexOf(":floor:",
                StringComparison.Ordinal);
            if (floorSeparator <= "encounter:".Length ||
                !int.TryParse(owner[(floorSeparator + ":floor:".Length)..],
                    NumberStyles.None, CultureInfo.InvariantCulture,
                    out var floor))
                throw new InvalidDataException(
                    "Native encounter formation owner is malformed.");
            var entry = owner["encounter:".Length..floorSeparator];
            return ChanceLawEngineV111.QueryNativeEncounterFormationIndex(
                entry, floor, indexRequest.SupportCount,
                indexRequest.Ordinal);
        }

        if (request.IndexRequest is { } potionModelRequest &&
            potionModelRequest.SiteId ==
                "potion_factory.model_within_rarity.index_draw")
        {
            var rarityDecision = continuation.Decisions.LastOrDefault(
                decision => decision.UnitFloatDecision is { } floatDecision &&
                    floatDecision.Request.SiteId ==
                        "potion_factory.rarity_roll.unit_float" &&
                    floatDecision.Request.OwnerId ==
                        potionModelRequest.OwnerId)?.UnitFloatDecision;
            if (rarityDecision is null ||
                continuation.Decisions.Any(decision =>
                    decision.IndexDecision is { } indexDecision &&
                    indexDecision.Request.SiteId ==
                        potionModelRequest.SiteId &&
                    indexDecision.Request.OwnerId ==
                        potionModelRequest.OwnerId))
                throw new InvalidDataException(
                    "Native combat-entry potion model request lacks its unique preceding rarity outcome.");
            var rarity = PrototypeNativePotionShop.ResolveRarityNative(
                rarityDecision.SelectedValue);
            return ChanceLawEngineV111.QueryNativePotionFactoryModelIndex(
                potionModelRequest, rarity, []);
        }

        if (request.UnitFloatRequest is { } rarityRequest)
            return ChanceLawEngineV111.QueryNativePotionFactoryRarity(
                rarityRequest);

        if (request.IndexRequest is { } selectionRequest)
        {
            var eventId = CanonicalJson.Sha256(new
            {
                boundary = "native-act-one-combat-entry-index",
                actionId = PrototypeAiEnvironment.StableActionId(action),
                request = selectionRequest
            });
            return ChanceLawEngineV111.QueryRandomIndex(
                eventId, selectionRequest);
        }

        throw new NotSupportedException(
            $"ReferenceNature Native typed run action cannot query '{request.SiteId}'.");
    }

    private static ChanceLawV111 QueryNativeRestHealRewardLaw(
        RunState source, GameAction action,
        ControlledStochasticContinuationStateV111 continuation,
        ReferenceNatureDecisionV111[] natureDecisions)
    {
        continuation.Validate();
        var request = continuation.PendingRequest;
        var typedDecisions = continuation.Decisions;
        if (!ControlledRunActionEngineV111.IsNativeRestHealRewardRequest(
                source, action, request) ||
            typedDecisions.Length != natureDecisions.Length ||
            source.World is not { } world)
            throw new InvalidDataException(
                "Native rest-heal reward request differs from its saved action prefix.");

        if (request.UnitFloatRequest is { SiteId: "card_rarity.roll.unit_float" }
            rarityRequest)
        {
            var (offset, priorRarityCount) =
                NativeRestHealRarityPrefix(source, typedDecisions,
                    natureDecisions);
            var pickIndex = NativeRestHealPickIndex(
                source, priorRarityCount);
            var expectedOwner =
                $"act:{world.Act}:room:Combat:pick:{pickIndex}:offset:{offset}";
            if (rarityRequest.OwnerId != expectedOwner)
                throw new InvalidDataException(
                    "Native rest-heal rarity owner differs from its saved offset prefix.");
            return ChanceLawEngineV111.QueryNativeCardRarity(
                PrototypeNativeCardOddsSource.RegularEncounter,
                offset, source.Ascension, rarityRequest.OwnerId,
                rarityRequest.Ordinal);
        }

        if (request.IndexRequest is { } modelRequest)
        {
            if (typedDecisions.Length == 0 ||
                typedDecisions[^1].UnitFloatDecision is not { } rarityDecision ||
                rarityDecision.Request.SiteId !=
                    "card_rarity.roll.unit_float" ||
                natureDecisions[^1].OutcomeId is not (
                    "rarity:common" or "rarity:uncommon" or "rarity:rare"))
                throw new InvalidDataException(
                    "Native rest-heal card model request lacks its preceding rarity outcome.");
            var rarityCount = typedDecisions.Count(decision =>
                decision.UnitFloatDecision?.Request.SiteId ==
                    "card_rarity.roll.unit_float");
            var pickIndex = NativeRestHealPickIndex(
                source, rarityCount - 1);
            var expectedOwnerPrefix =
                $"act:{world.Act}:room:Combat:pick:{pickIndex}:rarity:";
            if (!modelRequest.OwnerId.StartsWith(expectedOwnerPrefix,
                    StringComparison.Ordinal) ||
                !modelRequest.OwnerId.Contains(":pool:",
                    StringComparison.Ordinal))
                throw new InvalidDataException(
                    "Native rest-heal card model owner differs from its rarity prefix.");
            return ChanceLawEngineV111.QueryNativeEncounterCardModelIndex(
                modelRequest.OwnerId, modelRequest.SupportCount,
                modelRequest.Ordinal);
        }

        if (request.UnitFloatRequest is { } upgradeRequest &&
            upgradeRequest.SiteId ==
                "card_rarity.upgrade_roll.unit_float")
        {
            if (typedDecisions.Length == 0 ||
                typedDecisions[^1].IndexDecision?.Request.SiteId !=
                    "card_rarity.encounter_model.index_draw")
                throw new InvalidDataException(
                    "Native rest-heal card upgrade request lacks its preceding model choice.");
            var suffix =
                $":act:{world.Act}:ascension:{source.Ascension}";
            if (!upgradeRequest.OwnerId.StartsWith("card:",
                    StringComparison.Ordinal) ||
                !upgradeRequest.OwnerId.EndsWith(suffix,
                    StringComparison.Ordinal) ||
                upgradeRequest.OwnerId.Length <=
                    "card:".Length + suffix.Length)
                throw new InvalidDataException(
                    "Native rest-heal upgrade owner is malformed.");
            var cardId = upgradeRequest.OwnerId[
                "card:".Length..^suffix.Length];
            return ChanceLawEngineV111.QueryNativeEncounterCardUpgrade(
                cardId, world.Act, source.Ascension,
                upgradeRequest.Ordinal);
        }

        throw new NotSupportedException(
            $"ReferenceNature Native rest-heal reward cannot query '{request.SiteId}'.");
    }

    private static (int OffsetBasisPoints, int RarityCount)
        NativeRestHealRarityPrefix(
            RunState source,
            ControlledStochasticDecisionV111[] typedDecisions,
            ReferenceNatureDecisionV111[] natureDecisions)
    {
        if (source.World is not { } world ||
            typedDecisions.Length != natureDecisions.Length)
            throw new InvalidDataException(
                "Native rest-heal rarity prefix is missing its source world or Nature transcript.");
        var offset = world.CardRarityOffsetBasisPoints;
        var rarityCount = 0;
        for (var index = 0; index < typedDecisions.Length; index++)
        {
            var decision = typedDecisions[index];
            if (decision.UnitFloatDecision is not { } rarityDecision ||
                rarityDecision.Request.SiteId !=
                    "card_rarity.roll.unit_float")
                continue;
            var pickIndex = NativeRestHealPickIndex(source, rarityCount);
            var expectedOwner =
                $"act:{world.Act}:room:Combat:pick:{pickIndex}:offset:{offset}";
            if (rarityDecision.Request.StreamId !=
                    NativePlayerRngV111.RewardsStreamId ||
                rarityDecision.Request.OwnerId != expectedOwner)
                throw new InvalidDataException(
                    "Native rest-heal rarity transcript changed its offset owner.");
            offset = natureDecisions[index].OutcomeId switch
            {
                "rarity:rare" =>
                    PrototypeNativeCardRarityOdds.InitialOffsetBasisPoints,
                "rarity:common" or "rarity:uncommon" => Math.Min(
                    PrototypeNativeCardRarityOdds.MaximumOffsetBasisPoints,
                    offset + PrototypeNativeCardRarityOdds.GrowthBasisPoints(
                        source.Ascension)),
                _ => throw new InvalidDataException(
                    "Native rest-heal rarity transcript contains an invalid raw rarity.")
            };
            rarityCount++;
        }
        return (offset, rarityCount);
    }

    private static int NativeRestHealPickIndex(
        RunState source, int priorPickCount)
    {
        if (priorPickCount < 0)
            throw new ArgumentOutOfRangeException(nameof(priorPickCount));
        foreach (var count in source.Player.ActiveRelics
                     .Select(relic => PrototypeContent.Relic(relic.RelicId)
                         .RestHealCardRewardCount)
                     .Where(count => count > 0))
        {
            if (priorPickCount < count)
                return priorPickCount;
            priorPickCount -= count;
        }
        throw new InvalidDataException(
            "Native rest-heal rarity prefix exceeds the reward groups in the saved action.");
    }

    private ReferenceNatureRunActionSessionV111
        ResolveNextMapGenerationOutcome()
    {
        var mapGeneration = MapGeneration ?? throw new InvalidOperationException(
            "No ReferenceNature map-generation choice is pending.");
        var nextMapGeneration = mapGeneration.ResolveNext();
        var next = this with
        {
            Stochastic = Stochastic.Advance(
                nextMapGeneration.NatureState,
                nextMapGeneration.DecisionTranscript[^1]),
            MapGeneration = nextMapGeneration
        };
        if (nextMapGeneration.Complete)
            next = IsConfiguredExperimentalActTransitionAction(
                    Origin, Action)
                ? next.AdvanceAfterExperimentalActTransitionMap(
                    nextMapGeneration)
                : next.ContinueAfterOvergrowthRunStartMap(
                    nextMapGeneration);
        next.Validate();
        return next;
    }

    private ReferenceNatureRunActionSessionV111
        ResolveNextAncientOfferOutcome()
    {
        var ancientOffer = AncientOffer ?? throw new InvalidOperationException(
            "No ReferenceNature Ancient-offer choice is pending.");
        var nextAncientOffer = ancientOffer.ResolveNext();
        var next = this with
        {
            AncientOffer = nextAncientOffer,
            Stochastic = Stochastic.Advance(
                nextAncientOffer.NatureState,
                nextAncientOffer.DecisionTranscript[^1])
        };
        if (nextAncientOffer.Complete)
        {
            var mapGeneration = MapGeneration ??
                throw new InvalidDataException(
                    "Experimental act transition lost its completed map session.");
            var completedStep = new PrototypeGameEngine()
                .CompleteActTransitionStep(Origin, nextAncientOffer.Run);
            next = next.CompleteExperimentalActTransition(
                completedStep, mapGeneration,
                nextAncientOffer.NatureState,
                ReferenceNatureAncientOfferSessionV111.SaveSnapshot(
                    nextAncientOffer));
        }
        next.Validate();
        return next;
    }

    private ReferenceNatureRunActionSessionV111
        AdvanceAfterExperimentalActTransitionMap(
            ReferenceNatureMapGenerationSessionV111 mapGeneration)
    {
        if (!IsConfiguredExperimentalActTransitionAction(Origin, Action) ||
            !mapGeneration.Complete ||
            mapGeneration.Act != checked(Origin.World!.Act + 1) ||
            !mapGeneration.NatureState.SameCursor(NatureState))
            throw new InvalidOperationException(
                "Experimental act transition cannot commit before its map session completes.");
        var map = mapGeneration.Map ?? throw new InvalidDataException(
            "Completed experimental act-transition map session has no map.");
        var engine = new PrototypeGameEngine();
        var transitionState = engine.BeginActTransitionWithMap(
            Origin, Action, map);
        var siteId = AssignedAncientOfferSite(transitionState);
        if (siteId is not null)
        {
            var ancientOfferOrigin = ReferenceNatureRunCursorV111.Commit(
                transitionState, mapGeneration.NatureState);
            var ancientOffer = ReferenceNatureAncientOfferSessionV111.Begin(
                ancientOfferOrigin, siteId);
            var pending = this with
            {
                MapGeneration = mapGeneration,
                NatureState = mapGeneration.NatureState,
                AncientOffer = ancientOffer
            };
            pending.Validate();
            return pending;
        }
        var completedStep = engine.CompleteActTransitionStep(
            Origin, transitionState);
        return CompleteExperimentalActTransition(
            completedStep, mapGeneration, mapGeneration.NatureState);
    }

    private ReferenceNatureRunActionSessionV111
        CompleteExperimentalActTransition(
            RunState continued,
            ReferenceNatureMapGenerationSessionV111 mapGeneration,
            ReferenceNatureStateV111 finalNatureState,
            ReferenceNatureAncientOfferSessionSnapshotV111?
                ancientOfferAudit = null)
    {
        continued = CommitExperimentalActTransitionState(
            continued, mapGeneration, finalNatureState, ancientOfferAudit);
        return this with
        {
            Run = continued,
            NatureState = finalNatureState,
            MapGeneration = null,
            AncientOffer = null
        };
    }

    private static RunState CommitExperimentalActTransitionState(
        RunState continued,
        ReferenceNatureMapGenerationSessionV111 mapGeneration,
        ReferenceNatureStateV111 finalNatureState,
        ReferenceNatureAncientOfferSessionSnapshotV111?
            ancientOfferAudit)
    {
        var stochastic = continued.Stochastic
            .AppendReferenceNatureMapGenerationAudit(
                mapGeneration.ToAudit());
        if (ancientOfferAudit is not null)
            stochastic = stochastic.AppendReferenceNatureAncientOfferAudit(
                ancientOfferAudit);
        continued = continued with { Stochastic = stochastic };
        return ReferenceNatureRunCursorV111.Commit(
            continued, finalNatureState);
    }

    private static string? AssignedAncientOfferSite(RunState state)
    {
        var ancientId = state.World?.Stochastic.LaterActEncounterBag?
            .AssignedAncientId;
        return ReferenceNatureAncientOfferSessionV111
            .SiteForAssignedAncientId(ancientId);
    }

    private ReferenceNatureRunActionSessionV111
        ContinueAfterOvergrowthRunStartMap(
            ReferenceNatureMapGenerationSessionV111 mapGeneration)
    {
        if (Origin.Configuration?.UsesExperimentalSilentThreeActRoute != true)
            return CommitOvergrowthRunStartMap(mapGeneration);
        var preparation =
            ReferenceNatureLaterActPreparationSessionV111.Begin(
                Run.Stochastic.PrototypeRng, Origin.Ascension, Origin.Configuration.Profile,
                mapGeneration.NatureState);
        var next = this with
        {
            MapGeneration = mapGeneration,
            LaterActPreparation = preparation,
            NatureState = preparation.NatureState
        };
        return preparation.Complete
            ? next.CommitExperimentalThreeActRunStart()
            : next;
    }

    private ReferenceNatureRunActionSessionV111
        ResolveNextLaterActPreparationOutcome()
    {
        var preparation = LaterActPreparation ??
            throw new InvalidOperationException(
                "No ReferenceNature later-act preparation choice is pending.");
        var nextPreparation = preparation.ResolveNext();
        var next = this with
        {
            LaterActPreparation = nextPreparation,
            Stochastic = Stochastic.Advance(
                nextPreparation.NatureState,
                nextPreparation.DecisionTranscript[^1])
        };
        if (nextPreparation.Complete)
            next = next.CommitExperimentalThreeActRunStart();
        next.Validate();
        return next;
    }

    private ReferenceNatureRunActionSessionV111 CommitOvergrowthRunStartMap(
        ReferenceNatureMapGenerationSessionV111 mapGeneration)
    {
        var map = mapGeneration.Map ?? throw new InvalidDataException(
            "Completed Overgrowth run-start map session has no map.");
        var bossLaw = ChanceLawEngineV111.QueryOvergrowthBossEncounter(0);
        var mapLaws = mapGeneration.Decisions.Select(decision =>
            ChanceLawEngineV111.QueryNativeMapRequest(
                decision.ToRequest())).ToArray();
        var initializationTranscript =
            new ReferenceNatureRunInitializationTranscriptV111(
                InitialNatureState, mapGeneration.NatureState,
                new[] { bossLaw }.Concat(mapLaws).ToArray(),
                DecisionTranscript)
            {
                ActOneMapGeneration = mapGeneration.ToAudit()
            };
        initializationTranscript.Validate(replayMapGeneration: true);
        var runWithMap = Run with
        {
            World = (Run.World ?? throw new InvalidDataException(
                "Completed Overgrowth start has no world.")) with
            {
                Map = map
            }
        };
        var committedRun = runWithMap with
        {
            Stochastic = RunStochasticStateV111
                .CommitReferenceNatureRunInitialization(
                    runWithMap.Stochastic, mapGeneration.NatureState,
                    initializationTranscript, pending: false)
        };
        return this with
        {
            Run = committedRun,
            NatureState = mapGeneration.NatureState,
            MapGeneration = null
        };
    }

    private ReferenceNatureRunActionSessionV111
        CommitExperimentalThreeActRunStart()
    {
        var preparation = LaterActPreparation ??
            throw new InvalidDataException(
                "Experimental run start has no later-act preparation session.");
        preparation.Validate();
        if (!preparation.Complete)
            throw new InvalidOperationException(
                "Experimental run start cannot commit before its prepared bags finish.");
        var configuration = Origin.Configuration ??
            throw new InvalidDataException(
                "Experimental run start lost its configuration.");
        var bossLaw = configuration.Acts[0] == ActIdentity.Overgrowth
            ? ChanceLawEngineV111.QueryOvergrowthBossEncounter(0)
            : ChanceLawEngineV111.QueryUnderdocksBossEncounter(0);
        ReferenceNatureMapGenerationAuditV111? mapAudit = null;
        ChanceLawV111[] mapLaws = [];
        MapState? committedMap = null;
        if (configuration.Acts[0] == ActIdentity.Overgrowth)
        {
            var mapGeneration = MapGeneration ??
                throw new InvalidDataException(
                    "Experimental Overgrowth start lost its map session.");
            if (!mapGeneration.Complete)
                throw new InvalidOperationException(
                    "Experimental Overgrowth start cannot commit before its map finishes.");
            committedMap = mapGeneration.Map ?? throw new InvalidDataException(
                "Completed experimental Overgrowth start has no map.");
            mapAudit = mapGeneration.ToAudit();
            mapLaws = mapGeneration.Decisions.Select(decision =>
                ChanceLawEngineV111.QueryNativeMapRequest(
                    decision.ToRequest())).ToArray();
        }
        else if (MapGeneration is not null)
        {
            throw new InvalidDataException(
                "Experimental Underdocks start has an unexpected map session.");
        }

        var initializationLaws = new[] { bossLaw }
            .Concat(mapLaws)
            .Concat(preparation.ChanceLaws)
            .ToArray();
        var initializationTranscript =
            new ReferenceNatureRunInitializationTranscriptV111(
                InitialNatureState, preparation.NatureState,
                initializationLaws, DecisionTranscript)
            {
                ActOneMapGeneration = mapAudit,
                LaterActPreparationInitialPrototypeRng =
                    preparation.InitialPrototypeRng
            };
        initializationTranscript.Validate(
            replayMapGeneration: mapAudit is not null);
        var world = Run.World ?? throw new InvalidDataException(
            "Completed experimental start has no world.");
        var runWithPreparedBags = Run with
        {
            World = world with
            {
                Map = committedMap ?? world.Map,
                Stochastic = RunWorldStochasticStateV111
                    .CommitPreparedLaterActEncounterBags(
                        Run, preparation.Bags)
            }
        };
        var committedRun = runWithPreparedBags with
        {
            Stochastic = RunStochasticStateV111
                .CommitReferenceNatureRunInitialization(
                    runWithPreparedBags.Stochastic, preparation.NatureState,
                    initializationTranscript, pending: false)
        };
        return this with
        {
            Run = committedRun,
            NatureState = preparation.NatureState,
            MapGeneration = null,
            LaterActPreparation = null
        };
    }

    public void Validate()
    {
        var configuredNativeRunStart = Origin is not null
            && Action is not null
            && Action.Kind == "start_run"
            && ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureRunStartSource(Origin);
        var configuredOvergrowthRunStart = Origin is not null
            && Action is not null
            && Action.Kind == "start_run"
            && ControlledRunActionEngineV111
                .IsConfiguredNativeReferenceNatureOvergrowthRunStartSource(
                    Origin);
        var configuredExperimentalRunStart = Origin is not null &&
            configuredNativeRunStart
            && Origin.Configuration?.UsesExperimentalSilentThreeActRoute == true;
        var configuredExperimentalActTransition = Origin is not null
            && Action is not null
            && IsConfiguredExperimentalActTransitionAction(Origin, Action);
        var immediateShopAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111.IsShopPurchaseAction(
                Origin, Action)
            && DecisionTranscript is { Length: 0 };
        var immediateRewardRelicAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111.IsCombatRewardRelicAction(
                Origin, Action)
            && DecisionTranscript is { Length: 0 };
        var immediateRestHealRewardAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111
                .IsLegacyRestHealRewardAction(Origin, Action)
            && DecisionTranscript is { Length: 0 };
        var immediateOutsideCombatPotionAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111
                .IsSupportedOutsideCombatPotionAction(Origin, Action)
            && DecisionTranscript is { Length: 0 };
        var immediateActOneCombatAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111
                .IsLegacyActOneCombatEntryAction(Origin, Action)
            && DecisionTranscript is { Length: 0 };
        var immediateNativeActOneCombatAction = Origin is not null
            && Action is not null
            && ControlledRunActionEngineV111
                .IsNativeActOneCombatEntryAction(Origin, Action)
            && DecisionTranscript is { Length: 0 };
        var typedNativeRunAction = Origin is not null
            && Action is not null
            && UsesTypedRunAction(Origin, Action);
        if (SchemaId != CurrentSchemaId || Origin is null || Action is null ||
            Run is null || InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null || string.IsNullOrWhiteSpace(Action.Kind) ||
            (Run.Stochastic.ControlledRunActionChoice is null &&
             Run.Stochastic.ControlledRunActionHistory is not { Length: > 0 } &&
             !immediateShopAction && !immediateActOneCombatAction &&
             !immediateRewardRelicAction &&
             !immediateRestHealRewardAction &&
             !immediateOutsideCombatPotionAction &&
             !immediateNativeActOneCombatAction &&
             !typedNativeRunAction &&
             !configuredExperimentalActTransition) ||
            AncientOffer is not null &&
                !configuredExperimentalActTransition ||
            LegacyNativeAncientOfferCompletion &&
                (!configuredExperimentalActTransition ||
                 MapGeneration is not null ||
                 LaterActPreparation is not null ||
                 AncientOffer is not null))
            throw new InvalidDataException(
                "Invalid ReferenceNature run-action session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        Mechanics.Continuation.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunAction);
        Stochastic.Validate();
        if (ControlledRunActionEngineV111
                .IsLegacyNativeTreasureEntryAction(Origin, Action)
            || ControlledRunActionEngineV111
                .IsLegacyNativeShovelDigAction(Origin, Action))
        {
            ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
                Origin, "Native-regional relic-bag run-action origin");
            ReferenceNatureRunProjectionV111.ValidateNatureRelicBagProjection(
                Run, "Native-regional relic-bag run-action replay");
        }
        var actionMapNodeId = Action.Kind == "choose_map_node"
            ? Action.ReadPayload<ChooseMapNodePayload>().NodeId
            : null;
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature run-action cursor differs from the run envelope.");

        if (MapGeneration is { } mapGeneration)
        {
            mapGeneration.Validate();
            var mapDecisions = mapGeneration.DecisionTranscript;
            if (configuredExperimentalActTransition)
            {
                if (Complete || LaterActPreparation is not null ||
                    Run.Stochastic.ControlledRunActionChoice is not null ||
                    mapGeneration.Seed != Origin.RunSeed ||
                    mapGeneration.Act != checked(Origin.World!.Act + 1) ||
                    mapGeneration.Ascension != Origin.Ascension ||
                    !mapGeneration.InitialNatureState.SameCursor(
                        InitialNatureState) ||
                    CanonicalJson.Sha256(Run) !=
                        CanonicalJson.Sha256(Origin))
                    throw new InvalidDataException(
                        "Pending experimental act-transition map differs from its run-action transcript.");
                if (AncientOffer is null)
                {
                    if (mapGeneration.Complete ||
                        !mapGeneration.NatureState.SameCursor(NatureState) ||
                        !DecisionTranscript.SequenceEqual(mapDecisions))
                        throw new InvalidDataException(
                            "Pending experimental act-transition map differs from its run-action transcript.");
                }
                else
                {
                    AncientOffer.Validate();
                    if (!mapGeneration.Complete || AncientOffer.Complete ||
                        !AncientOffer.InitialNatureState.SameCursor(
                            mapGeneration.NatureState) ||
                        !AncientOffer.NatureState.SameCursor(NatureState) ||
                        !DecisionTranscript.Take(mapDecisions.Length)
                            .SequenceEqual(mapDecisions) ||
                        !DecisionTranscript.Skip(mapDecisions.Length)
                            .SequenceEqual(AncientOffer.DecisionTranscript))
                        throw new InvalidDataException(
                            "Pending experimental Ancient offer differs from its completed map prefix.");
                    var transitionState = new PrototypeGameEngine()
                        .BeginActTransitionWithMap(
                            Origin, Action,
                            mapGeneration.Map ?? throw new InvalidDataException(
                                "Completed transition map has no output."));
                    var siteId = AssignedAncientOfferSite(transitionState);
                    var expectedOfferOrigin = ReferenceNatureRunCursorV111.Commit(
                        transitionState, mapGeneration.NatureState);
                    if (siteId != AncientOffer.SiteId ||
                        CanonicalJson.Sha256(expectedOfferOrigin) !=
                            CanonicalJson.Sha256(AncientOffer.Origin))
                        throw new InvalidDataException(
                            "Pending experimental Ancient offer differs from its assigned transition boundary.");
                }
            }
            else
            {
                var expectedMapCursor = LaterActPreparation?.InitialNatureState
                    ?? NatureState;
                if (!configuredOvergrowthRunStart || Complete
                    || Run.Stochastic.ControlledRunActionChoice is not null
                    || Run.Stochastic
                        .ReferenceNatureRunInitializationTranscript is not null
                    || mapGeneration.Seed != Origin.RunSeed
                    || mapGeneration.Act != 1
                    || mapGeneration.Ascension != Origin.Ascension
                    || DecisionTranscript.Length < mapDecisions.Length + 1
                    || !DecisionTranscript.Skip(1).Take(mapDecisions.Length)
                        .SequenceEqual(mapDecisions)
                    || mapGeneration.Complete !=
                        (LaterActPreparation is not null)
                    || LaterActPreparation is null &&
                        DecisionTranscript.Length != mapDecisions.Length + 1)
                    throw new InvalidDataException(
                        "Pending Overgrowth map generation differs from its run-action transcript.");

                var bossLaw = ChanceLawEngineV111.QueryOvergrowthBossEncounter(0);
                var bossSelection = ReferenceNatureV111.SampleValidated(
                    bossLaw, InitialNatureState);
                var expectedBossDecision = new ReferenceNatureDecisionV111(
                    bossLaw.LawId, bossLaw.EventId,
                    bossSelection.OutcomeId, bossSelection.OutcomeIndex);
                var selectedBossId = Run.World?.Stochastic.ActOneEncounterPool?
                    .BossEncounterId;
                if (CanonicalJson.Sha256(DecisionTranscript[0]) !=
                        CanonicalJson.Sha256(expectedBossDecision)
                    || selectedBossId is null
                    || bossSelection.OutcomeId != "encounter:" + selectedBossId
                    || !bossSelection.NextState.SameCursor(
                        mapGeneration.InitialNatureState)
                    || !mapGeneration.NatureState.SameCursor(
                        expectedMapCursor))
                    throw new InvalidDataException(
                        "Pending Overgrowth map sampler does not follow the selected boss.");
            }
        }

        if (LaterActPreparation is { } laterActPreparation)
        {
            laterActPreparation.Validate();
            var config = Origin.Configuration;
            var bossLaw = config?.Acts[0] == ActIdentity.Overgrowth
                ? ChanceLawEngineV111.QueryOvergrowthBossEncounter(0)
                : ChanceLawEngineV111.QueryUnderdocksBossEncounter(0);
            var bossSelection = ReferenceNatureV111.SampleValidated(
                bossLaw, InitialNatureState);
            var selectedBossId = Run.World?.Stochastic.ActOneEncounterPool?
                .BossEncounterId;
            var expectedLaterActInitialState = bossSelection.NextState;
            var mapDecisionCount = 0;
            if (config?.Acts[0] == ActIdentity.Overgrowth)
            {
                if (MapGeneration is not { Complete: true } completedMap)
                    throw new InvalidDataException(
                        "Pending experimental Overgrowth initialization lost its completed map prefix.");
                mapDecisionCount = completedMap.DecisionTranscript.Length;
                if (!completedMap.InitialNatureState.SameCursor(
                        expectedLaterActInitialState))
                    throw new InvalidDataException(
                        "Experimental Overgrowth map does not follow its boss decision.");
                expectedLaterActInitialState = completedMap.NatureState;
            }
            if (!configuredExperimentalRunStart ||
                Run.Stochastic.ReferenceNatureRunInitializationPending != true ||
                Run.Stochastic.ControlledRunActionChoice is not null ||
                Run.Stochastic.ReferenceNatureRunInitializationTranscript
                    is not null ||
                selectedBossId is null ||
                bossSelection.OutcomeId != "encounter:" + selectedBossId ||
                laterActPreparation.Ascension != Origin.Ascension ||
                CanonicalJson.Sha256(laterActPreparation.InitialPrototypeRng) !=
                    CanonicalJson.Sha256(Run.Stochastic.PrototypeRng) ||
                CanonicalJson.Sha256(laterActPreparation.Profile) !=
                    CanonicalJson.Sha256(config!.Profile) ||
                !laterActPreparation.InitialNatureState.SameCursor(
                    expectedLaterActInitialState) ||
                DecisionTranscript.Length != 1 + mapDecisionCount +
                    laterActPreparation.DecisionTranscript.Length ||
                !DecisionTranscript.Skip(1 + mapDecisionCount).SequenceEqual(
                    laterActPreparation.DecisionTranscript) ||
                !laterActPreparation.NatureState.SameCursor(NatureState))
                throw new InvalidDataException(
                    "Pending experimental later-act preparation differs from the run-start transcript.");
        }

        if (Run.Stochastic.ReferenceNatureRunInitializationPending !=
            (configuredExperimentalRunStart &&
             (MapGeneration is not null || LaterActPreparation is not null)))
            throw new InvalidDataException(
                "Pending run initialization marker differs from its nested Nature session.");

        if (typedNativeRunAction)
        {
            ValidateNativeRunActionTranscript();
        }
        else if (NativeCombatEntryContinuation is not null ||
            NativeCombatEntryProviderDecisions.Length != 0)
        {
            throw new InvalidDataException(
                "Native typed stochastic continuation is attached to an unsupported run action.");
        }
        else if (Run.Stochastic.ControlledRunActionChoice is
            { } continuation)
        {
            continuation.Validate();
            if (continuation.ActionId != Action.Kind ||
                continuation.MapNodeId != actionMapNodeId ||
                (ControlledRunActionEngineV111.RequiresRunActionFingerprint(
                     Origin, Action)
                    ? continuation.RunAction is null ||
                      PrototypeAiEnvironment.StableActionId(
                          continuation.RunAction) !=
                      PrototypeAiEnvironment.StableActionId(Action)
                    : continuation.RunAction is not null) ||
                continuation.Decisions.Length != DecisionTranscript.Length)
                throw new InvalidDataException(
                    "Pending run-action continuation differs from ReferenceNature history.");
            for (int index = 0; index < continuation.Decisions.Length; index++)
                if (continuation.Decisions[index].ProviderMode !=
                        "controlled-v111" ||
                    continuation.Decisions[index].SelectedIndex !=
                        DecisionTranscript[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Pending run-action decisions differ from ReferenceNature history.");
        }
        else if (MapGeneration is not null ||
            LaterActPreparation is not null)
        {
            if (!configuredExperimentalActTransition)
            {
                var trace = Run.Stochastic.ControlledRunActionHistory![^1];
                trace.Validate();
                if (!configuredNativeRunStart || trace.ActionId != Action.Kind ||
                    trace.Decisions.Length != 1 ||
                    DecisionTranscript.Length < 1 ||
                    trace.Decisions[0].ProviderMode != "controlled-v111" ||
                    trace.Decisions[0].SelectedIndex !=
                        DecisionTranscript[0].OutcomeIndex)
                    throw new InvalidDataException(
                        "Pending run initialization boss choice differs from its controlled trace.");
            }
        }
        else if (configuredExperimentalActTransition &&
            LegacyNativeAncientOfferCompletion)
        {
            var legacyReplay = ReplayLegacyNativeAncientTransition(
                Origin, Action, InitialNatureState, DecisionTranscript);
            if (legacyReplay is null ||
                !legacyReplay.Value.MapGeneration.NatureState.SameCursor(
                    NatureState))
                throw new InvalidDataException(
                    "Legacy Native Ancient transition has an invalid map transcript.");
            var expectedWithoutMapAudit =
                ReferenceNatureRunCursorV111.Commit(
                    legacyReplay.Value.Run, NatureState);
            var expectedWithMapAudit = legacyReplay.Value.Run with
            {
                Stochastic = legacyReplay.Value.Run.Stochastic
                    .AppendReferenceNatureMapGenerationAudit(
                        legacyReplay.Value.MapGeneration.ToAudit())
            };
            expectedWithMapAudit = ReferenceNatureRunCursorV111.Commit(
                expectedWithMapAudit, NatureState);
            if (CanonicalJson.Sha256(expectedWithoutMapAudit) !=
                    CanonicalJson.Sha256(Run) &&
                CanonicalJson.Sha256(expectedWithMapAudit) !=
                    CanonicalJson.Sha256(Run))
                throw new InvalidDataException(
                    "Legacy Native Ancient transition differs from its saved run.");
        }
        else if (configuredExperimentalActTransition)
        {
            var experimentalMapGeneration =
                ReferenceNatureMapGenerationSessionV111.Begin(
                Origin.RunSeed, checked(Origin.World!.Act + 1),
                Origin.Ascension, InitialNatureState);
            var transcriptIndex = 0;
            while (!experimentalMapGeneration.Complete)
            {
                if (transcriptIndex >= DecisionTranscript.Length)
                    throw new InvalidDataException(
                        "Experimental act-transition transcript ended before its map.");
                experimentalMapGeneration =
                    experimentalMapGeneration.ResolveNext();
                if (!experimentalMapGeneration.DecisionTranscript[^1].Equals(
                        DecisionTranscript[transcriptIndex++]))
                    throw new InvalidDataException(
                        "Experimental act-transition map differs from its Nature transcript.");
            }
            var engine = new PrototypeGameEngine();
            var transitionState = engine.BeginActTransitionWithMap(
                Origin, Action,
                experimentalMapGeneration.Map ??
                    throw new InvalidDataException(
                    "Replayed act-transition map has no output."));
            var siteId = AssignedAncientOfferSite(transitionState);
            RunState transitioned;
            var finalNatureState =
                experimentalMapGeneration.NatureState;
            ReferenceNatureAncientOfferSessionSnapshotV111?
                ancientOfferAudit = null;
            if (siteId is null)
            {
                if (transcriptIndex != DecisionTranscript.Length)
                    throw new InvalidDataException(
                        "Experimental act-transition transcript has outcomes after its map.");
                transitioned = transitionState;
            }
            else
            {
                var offerOrigin = ReferenceNatureRunCursorV111.Commit(
                    transitionState,
                    experimentalMapGeneration.NatureState);
                var offer = ReferenceNatureAncientOfferSessionV111.Begin(
                    offerOrigin, siteId);
                while (!offer.Complete)
                {
                    if (transcriptIndex >= DecisionTranscript.Length)
                        throw new InvalidDataException(
                            "Experimental act-transition transcript ended before its assigned Ancient offer.");
                    offer = offer.ResolveNext();
                    if (!offer.DecisionTranscript[^1].Equals(
                            DecisionTranscript[transcriptIndex++]))
                        throw new InvalidDataException(
                            "Experimental assigned Ancient offer differs from its Nature transcript.");
                }
                transitioned = offer.Run;
                finalNatureState = offer.NatureState;
                ancientOfferAudit =
                    ReferenceNatureAncientOfferSessionV111.SaveSnapshot(offer);
            }
            if (transcriptIndex != DecisionTranscript.Length ||
                !finalNatureState.SameCursor(NatureState))
                throw new InvalidDataException(
                    "Completed experimental act-transition transcript is incomplete.");
            var expectedRun = engine.CompleteActTransitionStep(
                Origin, transitioned);
            expectedRun = CommitExperimentalActTransitionState(
                expectedRun, experimentalMapGeneration, NatureState,
                ancientOfferAudit);
            if (CanonicalJson.Sha256(expectedRun) !=
                CanonicalJson.Sha256(Run))
                throw new InvalidDataException(
                    "Completed experimental act transition differs from its map replay.");
        }
        else if (immediateShopAction || immediateRewardRelicAction ||
            immediateRestHealRewardAction ||
            immediateOutsideCombatPotionAction ||
            immediateActOneCombatAction ||
            immediateNativeActOneCombatAction)
        {
            var replayedRun = ControlledRunActionEngineV111.Begin(
                Origin.Fork(), CloneAction(Action));
            replayedRun = ReferenceNatureRunCursorV111.Commit(
                replayedRun, InitialNatureState);
            if (CanonicalJson.Sha256(replayedRun) !=
                CanonicalJson.Sha256(Run))
                throw new InvalidDataException(
                    "Deterministic ReferenceNature run action differs from its replay.");
            return;
        }
        else
        {
            var trace = Run.Stochastic.ControlledRunActionHistory![^1];
            trace.Validate();
            var expectedTraceDecisionCount = configuredNativeRunStart
                ? 1 : DecisionTranscript.Length;
            if (trace.ActionId != Action.Kind ||
                trace.MapNodeId != actionMapNodeId ||
                (ControlledRunActionEngineV111.RequiresRunActionFingerprint(
                     Origin, Action)
                    ? trace.ActionFingerprint !=
                      PrototypeAiEnvironment.StableActionId(Action)
                    : trace.ActionFingerprint is not null) ||
                trace.Decisions.Length != expectedTraceDecisionCount
                || DecisionTranscript.Length < trace.Decisions.Length)
                throw new InvalidDataException(
                    "Completed run-action trace differs from ReferenceNature history.");
            for (int index = 0; index < trace.Decisions.Length; index++)
                if (trace.Decisions[index].ProviderMode != "controlled-v111" ||
                    trace.Decisions[index].SelectedIndex !=
                        DecisionTranscript[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Completed run-action outcomes differ from ReferenceNature history.");

            if (configuredNativeRunStart)
            {
                var initialization =
                    Run.Stochastic.ReferenceNatureRunInitializationTranscript;
                var expectedBossSite = Origin.Configuration!.Acts[0] ==
                    ActIdentity.Overgrowth
                    ? "run.encounter.overgrowth_boss.index_draw"
                    : PrototypeNativeUnderdocks
                        .BossEncounterIndexSiteId;
                if (initialization is null
                    || initialization.ChanceLaws.Length == 0
                    || initialization.ChanceLaws[0].SiteId != expectedBossSite
                    || (initialization.ActOneMapGeneration is not null) !=
                        (Origin.Configuration.Acts[0] == ActIdentity.Overgrowth)
                    || initialization.DecisionTranscript.Length !=
                        DecisionTranscript.Length
                    || !initialization.DecisionTranscript.SequenceEqual(
                        DecisionTranscript)
                    || initialization.DecisionTranscript[0].OutcomeId !=
                        "encounter:" + Run.World?.Stochastic.ActOneEncounterPool?
                            .BossEncounterId)
                    throw new InvalidDataException(
                        "Completed Overgrowth run-start audit differs from its saved session transcript.");
            }
        }
    }

    private void ValidateNativeRunActionTranscript()
    {
        if (Run.Stochastic.ControlledRunActionChoice is not null ||
            MapGeneration is not null || LaterActPreparation is not null ||
            AncientOffer is not null ||
            NativeCombatEntryProviderDecisions.Length !=
                DecisionTranscript.Length ||
            NativeCombatEntryContinuation is { } savedContinuation &&
                CanonicalJson.Sha256(savedContinuation.Decisions) !=
                    CanonicalJson.Sha256(
                        NativeCombatEntryProviderDecisions))
            throw new InvalidDataException(
                "Native typed run-action continuation differs from its ReferenceNature transcript.");

        var expectedNatureState = InitialNatureState.Fork();
        for (var index = 0;
            index < NativeCombatEntryProviderDecisions.Length; index++)
        {
            var prefix = NativeCombatEntryProviderDecisions
                .Take(index).ToArray();
            var replayAtBoundary = CaptureNativeRunAction(
                Origin, Action, prefix);
            var boundary = replayAtBoundary.Continuation ??
                throw new InvalidDataException(
                    "Native run-action replay completed before its saved typed decision.");
            var savedDecision = NativeCombatEntryProviderDecisions[index];
            savedDecision.Validate();
            if (!ControlledStochasticChanceReplayV111.RequestsMatch(
                    boundary.PendingRequest, savedDecision.ToRequest()) ||
                !ControlledStochasticChanceReplayV111.CandidateLabelsMatch(
                    boundary.PendingCandidateLabels,
                    savedDecision.CandidateLabels))
                throw new InvalidDataException(
                    "Native run-action provider transcript changed its pending request.");

            var law = QueryNativeRunActionLaw(
                Origin, Action, boundary,
                DecisionTranscript.Take(index).ToArray());
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            var expectedDecision = ReferenceNatureChanceLawDecisionV111
                .FromSelection(
                    law, boundary.PendingRequest, selection, Origin,
                    boundary.PendingCandidateLabels);
            var expectedTranscript = new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex);
            if (CanonicalJson.Sha256(expectedDecision) !=
                    CanonicalJson.Sha256(savedDecision) ||
                CanonicalJson.Sha256(expectedTranscript) !=
                    CanonicalJson.Sha256(DecisionTranscript[index]))
                throw new InvalidDataException(
                    "Native run-action law decisions do not replay from the saved typed prefix.");
            expectedNatureState = selection.NextState;
        }

        var replay = CaptureNativeRunAction(
            Origin, Action, NativeCombatEntryProviderDecisions);
        if ((replay.Continuation is null) != Complete ||
            !SameNativeRunActionContinuation(
                replay.Continuation, NativeCombatEntryContinuation) ||
            !expectedNatureState.SameCursor(NatureState))
            throw new InvalidDataException(
                "Native run-action continuation differs from its replay.");
        var expectedRun = replay.Run is { } completed
            ? CommitNativeRunActionTrace(
                ReferenceNatureRunCursorV111.Commit(
                    completed, expectedNatureState), Action,
                NativeCombatEntryProviderDecisions)
            : Origin.Fork();
        if (CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "Native run-action result differs from its typed stochastic replay.");
    }

    private static bool SameNativeRunActionContinuation(
        ControlledStochasticContinuationStateV111? left,
        ControlledStochasticContinuationStateV111? right) =>
        left is null ? right is null : right is not null &&
            CanonicalJson.Sha256(left) == CanonicalJson.Sha256(right);

    public static string Save(ReferenceNatureRunActionSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureRunActionSessionSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin),
            CloneAction(session.Action), RunSnapshot.Save(session.Run),
            session.InitialNatureState, session.NatureState,
            (ReferenceNatureDecisionV111[])
                session.DecisionTranscript.Clone())
        {
            LegacyNativeAncientOfferCompletion =
                session.LegacyNativeAncientOfferCompletion,
            NativeCombatEntryProviderDecisions =
                session.NativeCombatEntryProviderDecisions
        };
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureRunActionSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureRunActionSessionSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature run-action snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            snapshot.Action is null ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature run-action snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var legacyAncientTransition = TryRestoreLegacyNativeAncientTransition(
            origin, snapshot.Action, savedRun,
            snapshot.InitialNatureState, snapshot.NatureState,
            snapshot.DecisionTranscript);
        if (legacyAncientTransition is not null)
            return legacyAncientTransition;
        if (snapshot.LegacyNativeAncientOfferCompletion)
            throw new InvalidDataException(
                "Legacy Native Ancient transition snapshot does not replay.");
        var replay = Begin(origin, snapshot.Action,
            snapshot.InitialNatureState);
        for (int index = 0; index < snapshot.DecisionTranscript.Length; index++)
        {
            if (replay.Complete)
                throw new InvalidDataException(
                    "ReferenceNature run-action replay ended before its transcript.");
            replay = replay.ResolveNext();
            if (CanonicalJson.Sha256(replay.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(snapshot.DecisionTranscript[index]))
                throw new InvalidDataException(
                    "ReferenceNature run-action replay selected a different outcome.");
        }
        if (replay.Complete &&
            savedRun.Stochastic.ReferenceNature is null)
            savedRun = ReferenceNatureRunCursorV111.Commit(
                savedRun, snapshot.NatureState);
        if (replay.Complete &&
            IsConfiguredExperimentalActTransitionAction(
                origin, snapshot.Action) &&
            savedRun.Stochastic.ReferenceNatureMapGenerationHistory is null &&
            replay.Run.Stochastic.ReferenceNatureMapGenerationHistory
                is { Length: > 0 }
                transitionMapHistory)
        {
            var stochastic = savedRun.Stochastic;
            foreach (var audit in transitionMapHistory)
                stochastic = stochastic
                    .AppendReferenceNatureMapGenerationAudit(audit);
            savedRun = savedRun with { Stochastic = stochastic };
        }
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            replay.DecisionTranscript.Length != snapshot.DecisionTranscript.Length ||
            (snapshot.NativeCombatEntryProviderDecisions is { } savedProviderDecisions
                ? CanonicalJson.Sha256(replay.NativeCombatEntryProviderDecisions) !=
                    CanonicalJson.Sha256(savedProviderDecisions)
                : replay.NativeCombatEntryProviderDecisions.Length != 0))
            throw new InvalidDataException(
                "ReferenceNature run-action replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static ReferenceNatureRunActionSessionV111?
        TryRestoreLegacyNativeAncientTransition(
            RunState origin, GameAction action, RunState savedRun,
            ReferenceNatureStateV111 initialNatureState,
            ReferenceNatureStateV111 natureState,
            ReferenceNatureDecisionV111[] decisionTranscript)
    {
        var replay = ReplayLegacyNativeAncientTransition(
            origin, action, initialNatureState, decisionTranscript);
        if (replay is null ||
            !replay.Value.MapGeneration.NatureState.SameCursor(natureState))
            return null;
        var expectedWithoutMapAudit = ReferenceNatureRunCursorV111.Commit(
            replay.Value.Run, natureState);
        var expectedWithMapAudit = replay.Value.Run with
        {
            Stochastic = replay.Value.Run.Stochastic
                .AppendReferenceNatureMapGenerationAudit(
                    replay.Value.MapGeneration.ToAudit())
        };
        expectedWithMapAudit = ReferenceNatureRunCursorV111.Commit(
            expectedWithMapAudit, natureState);
        if (CanonicalJson.Sha256(expectedWithoutMapAudit) !=
                CanonicalJson.Sha256(savedRun) &&
            CanonicalJson.Sha256(expectedWithMapAudit) !=
                CanonicalJson.Sha256(savedRun))
            return null;

        var session = new ReferenceNatureRunActionSessionV111(
            CurrentSchemaId, origin.Fork(), CloneAction(action),
            expectedWithMapAudit.Fork(), initialNatureState.Fork(),
            natureState.Fork(),
            (ReferenceNatureDecisionV111[])decisionTranscript.Clone())
        {
            LegacyNativeAncientOfferCompletion = true
        };
        session.Validate();
        return session;
    }

    private static (ReferenceNatureMapGenerationSessionV111 MapGeneration,
        RunState Run)? ReplayLegacyNativeAncientTransition(
            RunState origin, GameAction action,
            ReferenceNatureStateV111 initialNatureState,
            ReferenceNatureDecisionV111[] decisionTranscript)
    {
        if (!IsConfiguredExperimentalActTransitionAction(origin, action))
            return null;
        var mapGeneration = ReferenceNatureMapGenerationSessionV111.Begin(
            origin.RunSeed, checked(origin.World!.Act + 1), origin.Ascension,
            initialNatureState);
        var transcriptIndex = 0;
        while (!mapGeneration.Complete)
        {
            if (transcriptIndex >= decisionTranscript.Length)
                return null;
            mapGeneration = mapGeneration.ResolveNext();
            if (!mapGeneration.DecisionTranscript[^1].Equals(
                    decisionTranscript[transcriptIndex++]))
                return null;
        }
        if (transcriptIndex != decisionTranscript.Length)
            return null;
        var map = mapGeneration.Map ?? throw new InvalidDataException(
            "Legacy experimental transition map replay has no output.");
        var engine = new PrototypeGameEngine();
        var deferredTransition = engine.BeginActTransitionWithMap(
            origin, action, map);
        if (AssignedAncientOfferSite(deferredTransition) is null)
            return null;
        var completed = engine.StepActTransitionWithMapForReplay(
            origin.Fork(), action, map).State;
        completed = ReferenceNatureRunCursorV111.Commit(
            completed, mapGeneration.NatureState);
        return (mapGeneration, completed);
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

