using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureEndlessConveyorSessionSnapshotV111(
    string SchemaId,
    string OriginSnapshotJson,
    string RunSnapshotJson,
    string ActionId,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }
}

/// <summary>Atomic ReferenceNature continuation for Endless Conveyor dish,
/// potion, card-upgrade, and Fried Eel Colorless-card boundaries.</summary>
public sealed record ReferenceNatureEndlessConveyorSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    string ActionId,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    private ReferenceNatureDecisionV111[]? _decisionTranscript =
        ReferenceNatureSessionValueCopiesV111.CopyArray(DecisionTranscript);

    public ReferenceNatureDecisionV111[] DecisionTranscript
    {
        get => ReferenceNatureSessionValueCopiesV111
            .CopyArray(_decisionTranscript)!;
        init => _decisionTranscript = ReferenceNatureSessionValueCopiesV111
            .CopyArray(value);
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-endless-conveyor-session-v1";

    public static string[] SessionSiteIds =>
        [PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
         PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
         PrototypeNativeEndlessConveyor.ColorlessCardSiteId,
         PrototypeNativePotionShop.UniformModelSiteId];

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length > 0 &&
        DecisionTranscript.Length == PlannedSiteIds.Length;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null : QuerySite(
        Origin, ActionId, PlannedSiteIds[NextOrdinal]);

    public static ReferenceNatureEndlessConveyorSessionV111 Begin(
        RunState origin, string actionId, ulong natureSeed) => Begin(origin,
            actionId, ReferenceNatureRunCursorV111.Begin(
                origin, natureSeed, "Endless Conveyor"));

    public static ReferenceNatureEndlessConveyorSessionV111 Begin(
        RunState origin, string actionId) => Begin(origin, actionId,
            ReferenceNatureRunCursorV111.Begin(
                origin, null, "Endless Conveyor"));

    private static ReferenceNatureEndlessConveyorSessionV111 Begin(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initial)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(initial);
        var firstSiteId = GetPlannedSiteIds(origin, actionId)[0];
        _ = QuerySite(origin, actionId, firstSiteId);
        ReferenceNatureRunCursorV111.ValidateInitial(
            origin, initial, "Endless Conveyor");
        var session = new ReferenceNatureEndlessConveyorSessionV111(
            CurrentSchemaId, origin.Fork(), origin.Fork(), actionId,
            initial.Fork(), initial.Fork(), []);
        session.Validate();
        return session;
    }

    public ReferenceNatureEndlessConveyorSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Origin = Origin.Fork(),
            Run = Run.Fork(),
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            DecisionTranscript = (ReferenceNatureDecisionV111[])
                DecisionTranscript.Clone()
        };
    }

    public ReferenceNatureEndlessConveyorSessionV111 ResolveNext()
    {
        Validate();
        if (Complete)
            throw new InvalidOperationException(
                "ReferenceNature Endless Conveyor session is already complete.");
        var plannedSiteIds = PlannedSiteIds;
        var law = QuerySite(
            Origin, ActionId, plannedSiteIds[NextOrdinal]);
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var nextNatureState = selection.NextState;
        var nextRun = Origin.Fork();
        if (transcript.Length == plannedSiteIds.Length)
        {
            var selections = SampleTranscript(
                Origin, ActionId, InitialNatureState, transcript,
                plannedSiteIds);
            nextRun = ReferenceNatureRunCursorV111.Commit(
                ReplaySelections(Origin, ActionId, selections),
                nextNatureState);
        }
        var next = this with
        {
            Run = nextRun,
            NatureState = nextNatureState,
            DecisionTranscript = transcript
        };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            string.IsNullOrWhiteSpace(ActionId) ||
            InitialNatureState is null || NatureState is null ||
            DecisionTranscript is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature Endless Conveyor session envelope.");
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        InitialNatureState.Validate();
        NatureState.Validate();
        _ = RequireAction(Origin, ActionId);
        var plannedSiteIds = PlannedSiteIds;
        if (DecisionTranscript.Length > plannedSiteIds.Length ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Endless Conveyor cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        var selections = SampleTranscript(
            Origin, ActionId, InitialNatureState, DecisionTranscript,
            plannedSiteIds, out expectedNatureState);
        var expectedRun = Complete
            ? ReferenceNatureRunCursorV111.Commit(
                ReplaySelections(Origin, ActionId, selections),
                expectedNatureState)
            : Origin.Fork();

        if (CanonicalJson.Sha256(expectedNatureState) !=
                CanonicalJson.Sha256(NatureState) ||
            CanonicalJson.Sha256(expectedRun) != CanonicalJson.Sha256(Run))
            throw new InvalidDataException(
                "ReferenceNature Endless Conveyor replay differs from its session envelope.");
    }

    public static string Save(
        ReferenceNatureEndlessConveyorSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        return JsonSerializer.Serialize(
            new ReferenceNatureEndlessConveyorSessionSnapshotV111(
                CurrentSchemaId,
                JsonSerializer.Serialize(session.Origin),
                JsonSerializer.Serialize(session.Run),
                session.ActionId,
                session.InitialNatureState,
                session.NatureState,
                session.DecisionTranscript),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public static ReferenceNatureEndlessConveyorSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
            ReferenceNatureEndlessConveyorSessionSnapshotV111>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException(
                "Invalid ReferenceNature Endless Conveyor snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId)
            throw new InvalidDataException(
                "Unsupported ReferenceNature Endless Conveyor snapshot schema.");
        var origin = JsonSerializer.Deserialize<RunState>(
            snapshot.OriginSnapshotJson)
            ?? throw new InvalidDataException(
                "Endless Conveyor snapshot has no origin state.");
        var run = JsonSerializer.Deserialize<RunState>(
            snapshot.RunSnapshotJson)
            ?? throw new InvalidDataException(
                "Endless Conveyor snapshot has no current state.");
        var session = new ReferenceNatureEndlessConveyorSessionV111(
            snapshot.SchemaId, origin, run, snapshot.ActionId,
            snapshot.InitialNatureState, snapshot.NatureState,
            snapshot.DecisionTranscript);
        session.Validate();
        return session;
    }

    private static GameAction RequireAction(RunState origin, string actionId)
    {
        var action = new PrototypeGameEngine().GetLegalActions(origin)
            .SingleOrDefault(candidate =>
                PrototypeAiEnvironment.StableActionId(candidate) == actionId);
        if (action is null)
            throw new InvalidOperationException(
                "Endless Conveyor Nature choice requires its current legal action.");
        if (origin.Phase == RunPhase.MapChoice)
        {
            var world = origin.World
                ?? throw new InvalidOperationException(
                    "Endless Conveyor map entry requires a world.");
            var nodeId = action.Kind == "choose_map_node"
                ? action.ReadPayload<ChooseMapNodePayload>().NodeId
                : null;
            if (nodeId is null || !PrototypeNativeWingedBoots
                    .AvailableNodes(world, origin.Player).Any(node =>
                        node.NodeId == nodeId &&
                        node.RoomType == PrototypeRoomType.Event))
                throw new InvalidOperationException(
                    "Endless Conveyor Nature choice requires a legal Event-node action.");
        }
        else if (origin.Phase != RunPhase.Event ||
            origin.World?.Event?.EventId != PrototypeNativeEndlessConveyor.EventId)
        {
            throw new InvalidOperationException(
                "Endless Conveyor Nature choice requires its active event action.");
        }
        return action;
    }

    private string[] PlannedSiteIds => GetPlannedSiteIds(Origin, ActionId);

    [JsonIgnore]
    public string? LastSiteId => DecisionTranscript.Length == 0
        ? null : PlannedSiteIds[DecisionTranscript.Length - 1];

    private static string[] GetPlannedSiteIds(
        RunState origin, string actionId)
    {
        var action = RequireAction(origin, actionId);
        var evt = origin.World?.Event;
        if (origin.Phase == RunPhase.Event && evt is not null &&
            action.Kind == "event_choice")
        {
            var choice = action.ReadPayload<EventChoicePayload>().ChoiceId;
            if (choice == "observe")
                return [PrototypeNativeEndlessConveyor.UpgradeCardSiteId];
            if (choice == "grab" && evt.NativeDishId == "FRIED_EEL" &&
                origin.Player.Gold >= 40)
                return [PrototypeNativeEndlessConveyor.ColorlessCardSiteId,
                    PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId];
            if (choice == "grab" && evt.NativeDishId == "SPICY_SNAPPY" &&
                origin.Player.Gold >= 40 &&
                PrototypeNativeEndlessConveyor
                    .GetUpgradeCardCandidates(origin).Length > 0)
                return [PrototypeNativeEndlessConveyor.UpgradeCardSiteId,
                    PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId];
            if (choice == "grab" &&
                evt.NativeDishId == "SUSPICIOUS_CONDIMENT")
                return [PrototypeNativePotionShop.UniformModelSiteId];
        }
        return [PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId];
    }

    private static ChanceLawV111 QuerySite(
        RunState origin, string actionId, string siteId) => siteId switch
    {
        PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId =>
            ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry(
                origin, actionId),
        PrototypeNativeEndlessConveyor.UpgradeCardSiteId =>
            ChanceLawEngineV111.QueryNativeEndlessConveyorUpgrade(
                origin, actionId),
        PrototypeNativeEndlessConveyor.ColorlessCardSiteId =>
            ChanceLawEngineV111.QueryNativeEndlessConveyorColorlessCard(
                origin, actionId),
        PrototypeNativePotionShop.UniformModelSiteId =>
            QuerySuspiciousCondimentPotion(origin, actionId),
        _ => throw new NotSupportedException(
            "Endless Conveyor session has no query for site '" + siteId + "'.")
    };

    private static ChanceLawV111 QuerySuspiciousCondimentPotion(
        RunState origin, string actionId)
    {
        var action = RequireAction(origin, actionId);
        var evt = origin.World?.Event;
        if (origin.Phase != RunPhase.Event || action.Kind != "event_choice" ||
            action.ReadPayload<EventChoicePayload>().ChoiceId != "grab" ||
            evt is null ||
            evt.EventId != PrototypeNativeEndlessConveyor.EventId ||
            evt.NativeDishId != "SUSPICIOUS_CONDIMENT" ||
            evt.NativeDishRollPending ||
            evt.PendingPotionReplacement is not null ||
            evt.RemainingPotionIds.Length != 0 ||
            string.IsNullOrWhiteSpace(origin.RunSeed) ||
            origin.NativeRunRng is null ||
            evt.NativeEventRng?.StreamId != NativeRngV111Contexts
                .ForEvent(origin.RunSeed,
                    PrototypeNativeEndlessConveyor.NativeRngEventId).StreamId ||
            !origin.Player.PotionSlots.Any(slot => slot is null))
            throw new InvalidOperationException(
                "Suspicious Condiment potion law requires its legal Grab action before the queued potion is resolved.");

        var ownerPrefix =
            $"event:endless_conveyor:dish:{evt.NativeDishCount}:potion";
        return ChanceLawEngineV111.QueryLegacyUniformPotionModel(
            "reward", ownerPrefix,
            GetLegacyStreamOrdinal(origin, "reward"));
    }

    private static int GetLegacyStreamOrdinal(
        RunState origin, string streamId)
    {
        var streams = origin.Rng.Streams.Where(stream =>
            stream.StreamId == streamId).ToArray();
        if (streams.Length != 1)
            throw new InvalidDataException(
                $"Endless Conveyor potion request requires one '{streamId}' legacy stream.");
        var ordinal = streams[0].CallCount ?? 0;
        if (ordinal > int.MaxValue)
            throw new NotSupportedException(
                "Endless Conveyor potion request ordinal exceeds the typed request range.");
        return (int)ordinal;
    }

    private static (ChanceLawV111 Law,
        ReferenceNatureSelectionV111 Selection)[] SampleTranscript(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureDecisionV111[] transcript,
        string[] plannedSiteIds) => SampleTranscript(
            origin, actionId, initialNatureState, transcript,
            plannedSiteIds, out _);

    private static (ChanceLawV111 Law,
        ReferenceNatureSelectionV111 Selection)[] SampleTranscript(
        RunState origin, string actionId,
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureDecisionV111[] transcript,
        string[] plannedSiteIds,
        out ReferenceNatureStateV111 finalNatureState)
    {
        if (transcript.Length > plannedSiteIds.Length)
            throw new InvalidDataException(
                "Endless Conveyor transcript exceeds its planned event boundaries.");
        var cursor = initialNatureState.Fork();
        var selections = new List<(ChanceLawV111,
            ReferenceNatureSelectionV111)>(transcript.Length);
        for (var index = 0; index < transcript.Length; index++)
        {
            var law = QuerySite(origin, actionId, plannedSiteIds[index]);
            var selection = ReferenceNatureV111.SampleValidated(law, cursor);
            transcript[index].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(transcript[index]))
                throw new InvalidDataException(
                    "ReferenceNature Endless Conveyor transcript does not replay.");
            selections.Add((law, selection));
            cursor = selection.NextState;
        }
        finalNatureState = cursor;
        return selections.ToArray();
    }

    private static RunState ReplaySelections(
        RunState origin, string actionId,
        (ChanceLawV111 Law,
            ReferenceNatureSelectionV111 Selection)[] selections)
    {
        if (selections.Length == 1)
        {
            var (law, selection) = selections[0];
            if (law.SiteId == PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId)
                return ReplayDishSelection(origin, actionId, law, selection);
            if (law.OperationKind == "choose_index")
                return ReplayIndexSelection(origin, actionId, law, selection);
            throw new InvalidDataException(
                "Unsupported single-boundary Endless Conveyor Nature transcript.");
        }
        if (selections.Length != 2 ||
            selections[0].Law.OperationKind != "choose_index" ||
            selections[1].Law.SiteId != PrototypeNativeEndlessConveyor
                .DishWeightedTicketSiteId)
            throw new InvalidDataException(
                "Endless Conveyor mixed transcript must be one index followed by one dish ticket.");

        var indexChoice = selections[0];
        var dishChoice = selections[1];
        var indexRequest = BuildIndexRequest(origin, indexChoice.Law);
        var typedIndex = ReferenceNatureChanceLawDecisionV111.FromSelection(
            indexChoice.Law,
            ControlledStochasticRequestV111.FromIndex(indexRequest),
            indexChoice.Selection).IndexDecision!;
        var preDraw = CaptureDishPreDrawState(origin, actionId);
        var floatRequest = BuildUnitFloatRequest(origin, dishChoice.Law);
        var typedFloat = ReferenceNatureChanceLawDecisionV111.FromSelection(
            dishChoice.Law,
            ControlledStochasticRequestV111.FromUnitFloat(floatRequest),
            dishChoice.Selection, preDraw).UnitFloatDecision!;
        var run = ReferenceNatureNativeEventIndexReplayV111
            .ReplayMixedIndexAndUnitFloat(
                origin, typedIndex, typedFloat,
                RequireAction(origin, actionId),
                PrototypeNativeEndlessConveyor.NativeRngEventId,
                PrototypeNativeEndlessConveyor.EventId,
                indexChoice.Law.SiteId,
                dishChoice.Law.SiteId);
        ValidateSelectedEffect(origin, run, indexChoice.Law,
            indexChoice.Selection);
        ValidateSelectedDish(run, dishChoice.Selection);
        return run;
    }

    private static RunState ReplayDishSelection(
        RunState origin, string actionId, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var conditioningRun = origin.Phase == RunPhase.MapChoice
            ? origin : CaptureDishPreDrawState(origin, actionId);
        var request = BuildUnitFloatRequest(origin, law);
        var controlled = ReferenceNatureChanceLawDecisionV111.FromSelection(
            law, ControlledStochasticRequestV111.FromUnitFloat(request),
            selection, conditioningRun);
        var run = ReferenceNatureNativeEventIndexReplayV111.ReplayUnitFloat(
            origin,
            controlled.UnitFloatDecision! with
            {
                ProviderMode = "scripted-v111"
            },
            RequireAction(origin, actionId),
            PrototypeNativeEndlessConveyor.NativeRngEventId,
            PrototypeNativeEndlessConveyor.EventId,
            PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId,
            GetPreDrawDishCount(law));
        ValidateSelectedDish(run, selection);
        return run;
    }

    private static RunState ReplayIndexSelection(
        RunState origin, string actionId, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var request = BuildIndexRequest(origin, law);
        var controlledRequest =
            ControlledStochasticRequestV111.FromIndex(request);
        var selected = law.SiteId ==
                PrototypeNativePotionShop.UniformModelSiteId
            ? ReferenceNatureChanceLawDecisionV111.FromOrderedIndexSelection(
                law, controlledRequest, selection,
                law.Outcomes.Select(outcome => outcome.OutcomeId).ToArray())
            : ReferenceNatureChanceLawDecisionV111.FromSelection(
                law, controlledRequest, selection);
        var action = RequireAction(origin, actionId);
        var run = ReferenceNatureNativeEventIndexReplayV111
            .ReplayControlledIndex(
                origin, request, selected,
                checkpoint => new PrototypeGameEngine().Step(
                    checkpoint, action).State,
                PrototypeNativeEndlessConveyor.NativeRngEventId,
                PrototypeNativeEndlessConveyor.EventId);
        ValidateSelectedEffect(origin, run, law, selection);
        return run;
    }

    private static RandomIndexRequestV111 BuildIndexRequest(
        RunState origin, ChanceLawV111 law)
    {
        if (!ReferenceNatureEndlessConveyorSessionV111.SessionSiteIds.Contains(
                law.SiteId, StringComparer.Ordinal))
            throw new NotSupportedException(
                $"Endless Conveyor index replay does not support site '{law.SiteId}'.");
        if (law.SiteId == PrototypeNativePotionShop.UniformModelSiteId)
        {
            var request = new RandomIndexRequestV111(
                law.SiteId, "reward", law.OwnerId, law.Ordinal,
                Enumerable.Range(0, law.Outcomes.Length).ToArray());
            request.Validate();
            return request;
        }
        var stream = origin.World?.Event?.NativeEventRng
            ?? throw new InvalidDataException(
                "Endless Conveyor index replay lost its Native event cursor.");
        return new RandomIndexRequestV111(
            law.SiteId, stream.StreamId, law.OwnerId, law.Ordinal,
            Enumerable.Range(0, law.Outcomes.Length).ToArray());
    }

    private static RandomUnitFloatRequestV111 BuildUnitFloatRequest(
        RunState origin, ChanceLawV111 law) => new(
            law.SiteId,
            origin.World?.Event?.NativeEventRng?.StreamId ??
                NativeRngV111Contexts.ForEvent(
                    origin.RunSeed,
                    PrototypeNativeEndlessConveyor.NativeRngEventId).StreamId,
            law.OwnerId,
            law.Ordinal);

    private static void ValidateSelectedEffect(
        RunState origin, RunState run, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        if (law.SiteId == PrototypeNativeEndlessConveyor.UpgradeCardSiteId)
        {
            var candidates = PrototypeNativeEndlessConveyor
                .GetUpgradeCardCandidates(origin);
            if ((uint)selection.OutcomeIndex >= (uint)candidates.Length ||
                law.Outcomes[selection.OutcomeIndex].OutcomeId !=
                    $"card-instance:{candidates[selection.OutcomeIndex]}:index:{selection.OutcomeIndex}")
                throw new InvalidDataException(
                    "Endless Conveyor upgrade law differs from its ordered card support.");
            var selectedInstanceId = candidates[selection.OutcomeIndex];
            var expectedDeck = origin.Player.Deck.Select(card =>
                card.InstanceId == selectedInstanceId
                    ? card with { UpgradeLevel = card.UpgradeLevel + 1 }
                    : card).ToArray();
            if (!run.Player.Deck.SequenceEqual(expectedDeck))
                throw new InvalidDataException(
                    "Scripted Endless Conveyor action upgraded a different card than its Nature outcome.");
            return;
        }

        if (law.SiteId == PrototypeNativeEndlessConveyor.ColorlessCardSiteId)
        {
            var candidates = PrototypeNativeEndlessConveyor
                .GetColorlessCardCandidates();
            if ((uint)selection.OutcomeIndex >= (uint)candidates.Length ||
                law.Outcomes[selection.OutcomeIndex].OutcomeId !=
                    $"card-model:{candidates[selection.OutcomeIndex]}:index:{selection.OutcomeIndex}")
                throw new InvalidDataException(
                    "Endless Conveyor Colorless law differs from its ordered card support.");
            var cardId = candidates[selection.OutcomeIndex];
            var world = origin.World
                ?? throw new InvalidDataException(
                    "Endless Conveyor Colorless replay lost its world.");
            if (run.Player.Deck.Length != origin.Player.Deck.Length + 1 ||
                run.Player.Deck[^1].InstanceId != world.NextCardInstanceId ||
                run.Player.Deck[^1].CardId != cardId)
                throw new InvalidDataException(
                    "Scripted Fried Eel added a different Colorless card than its Nature outcome.");
            return;
        }

        if (law.SiteId == PrototypeNativePotionShop.UniformModelSiteId)
        {
            var candidates = PrototypeNativePotionShop.NativePotionPoolOrder;
            var eventState = run.World?.Event;
            if ((uint)selection.OutcomeIndex >= (uint)candidates.Count ||
                law.Outcomes[selection.OutcomeIndex].OutcomeId !=
                    "potion:" + candidates[selection.OutcomeIndex] ||
                run.Phase != RunPhase.Event || eventState is null ||
                eventState.EventId != PrototypeNativeEndlessConveyor.EventId ||
                eventState.NativePageIndex != 1 ||
                eventState.ChosenChoiceId != "grab" ||
                eventState.NativeDishId != "SUSPICIOUS_CONDIMENT" ||
                !eventState.NativeDishRollPending ||
                eventState.PendingPotionReplacement?.PotionId !=
                    candidates[selection.OutcomeIndex] ||
                run.Player.Gold != origin.Player.Gold - 40 ||
                !run.Player.PotionSlots.SequenceEqual(
                    origin.Player.PotionSlots) ||
                CanonicalJson.Sha256(run.Rng) !=
                    CanonicalJson.Sha256(origin.Rng))
                throw new InvalidDataException(
                    "Scripted Suspicious Condiment action did not queue the Nature-selected potion without advancing the legacy provider.");
            return;
        }

        throw new InvalidDataException(
            "Unknown Endless Conveyor selected event effect site '" + law.SiteId + "'.");
    }

    private static void ValidateSelectedDish(
        RunState run, ReferenceNatureSelectionV111 selection)
    {
        if (!selection.OutcomeId.StartsWith("dish:", StringComparison.Ordinal) ||
            run.World?.Event?.NativeDishId != selection.OutcomeId[5..])
            throw new InvalidDataException(
                "Scripted Endless Conveyor ticket resolved a different dish than its Nature outcome.");
    }

    private static RunState CaptureDishPreDrawState(
        RunState origin, string actionId)
    {
        var action = RequireAction(origin, actionId);
        IReadOnlyList<NativeEventUnitFloatCaptureV111> captures;
        using (var probe = NativeEventRandomProbeV111.Begin())
        using (RandomBoundaryJournalV111.Begin())
        using (NativeEngineEventJournalV111.Begin())
        {
            _ = new PrototypeGameEngine().Step(origin.Fork(), action).State;
            captures = probe.UnitFloatCaptures;
        }
        var matching = captures.Where(capture =>
            capture.Request.SiteId == PrototypeNativeEndlessConveyor
                .DishWeightedTicketSiteId).ToArray();
        if (matching.Length != 1)
            throw new InvalidDataException(
                "Endless Conveyor action no longer reaches exactly one weighted dish ticket.");
        return matching[0].PreDrawState;
    }

    private static int GetPreDrawDishCount(ChanceLawV111 law)
    {
        var parts = law.OwnerId.Split(':');
        if (parts.Length != 4 || parts[0] != "dish" ||
            !int.TryParse(parts[1],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var nextRoll) || nextRoll <= 0)
            throw new InvalidDataException(
                "Endless Conveyor law has a malformed dish owner identity.");
        return nextRoll - 1;
    }
}
