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
    ReferenceNatureDecisionV111[] DecisionTranscript);

/// <summary>Atomic ReferenceNature continuation for one weighted Endless
/// Conveyor dish ticket during event entry or a dish-action continuation.</summary>
public sealed record ReferenceNatureEndlessConveyorSessionV111(
    string SchemaId,
    RunState Origin,
    RunState Run,
    string ActionId,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureDecisionV111[] DecisionTranscript)
{
    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-endless-conveyor-session-v1";

    public static string[] SessionSiteIds =>
        [PrototypeNativeEndlessConveyor.DishWeightedTicketSiteId];

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public int NextOrdinal => DecisionTranscript.Length;

    [JsonIgnore]
    public bool Complete => DecisionTranscript.Length == 1;

    [JsonIgnore]
    public ChanceLawV111? PendingLaw => Complete ? null :
        ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry(
            Origin, ActionId);

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
        _ = ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry(
            origin, actionId);
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
        var law = ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry(
            Origin, ActionId);
        var selection = ReferenceNatureV111.SampleValidated(
            law, NatureState);
        var transcript = DecisionTranscript.Append(
            new ReferenceNatureDecisionV111(
                law.LawId, law.EventId, selection.OutcomeId,
                selection.OutcomeIndex)).ToArray();
        var replay = ReplaySelection(Origin, ActionId, law, selection);
        var next = this with
        {
            Run = ReferenceNatureRunCursorV111.Commit(
                replay, selection.NextState),
            NatureState = selection.NextState,
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
        var law = ChanceLawEngineV111.QueryNativeEndlessConveyorDishEntry(
            Origin, ActionId);
        if (DecisionTranscript.Length > 1 ||
            !ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                DecisionTranscript.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature Endless Conveyor cursor differs from its session envelope.");

        var expectedNatureState = InitialNatureState.Fork();
        RunState expectedRun;
        if (Complete)
        {
            var selection = ReferenceNatureV111.SampleValidated(
                law, expectedNatureState);
            DecisionTranscript[0].Validate();
            if (CanonicalJson.Sha256(new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex)) !=
                CanonicalJson.Sha256(DecisionTranscript[0]))
                throw new InvalidDataException(
                    "ReferenceNature Endless Conveyor transcript does not replay.");
            expectedNatureState = selection.NextState;
            expectedRun = ReplaySelection(
                Origin, ActionId, law, selection);
            expectedRun = ReferenceNatureRunCursorV111.Commit(
                expectedRun, expectedNatureState);
        }
        else
        {
            expectedRun = Origin.Fork();
        }

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

    private static RunState ReplaySelection(
        RunState origin, string actionId, ChanceLawV111 law,
        ReferenceNatureSelectionV111 selection)
    {
        var conditioningRun = origin.Phase == RunPhase.MapChoice
            ? origin : CaptureDishPreDrawState(origin, actionId);
        var request = new RandomUnitFloatRequestV111(
            law.SiteId,
            NativeRngV111Contexts.ForEvent(
                origin.RunSeed,
                PrototypeNativeEndlessConveyor.NativeRngEventId).StreamId,
            law.OwnerId,
            law.Ordinal);
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
        if (!selection.OutcomeId.StartsWith("dish:", StringComparison.Ordinal) ||
            run.World?.Event?.NativeDishId != selection.OutcomeId[5..])
            throw new InvalidDataException(
                "Scripted Endless Conveyor ticket resolved a different dish than its Nature outcome.");
        return run;
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
