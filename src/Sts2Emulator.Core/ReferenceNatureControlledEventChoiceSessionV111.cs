using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record ReferenceNatureControlledEventChoiceStepV111(
    string Kind, ReferenceNatureDecisionV111? NatureDecision = null,
    GameAction? PlayerAction = null)
{
    public void Validate()
    {
        if (Kind == "nature")
        {
            if (NatureDecision is null || PlayerAction is not null)
                throw new InvalidDataException(
                    "Malformed ReferenceNature event-choice sample step.");
            NatureDecision.Validate();
            return;
        }
        if (Kind == "player_action")
        {
            if (PlayerAction is null || NatureDecision is not null ||
                string.IsNullOrWhiteSpace(PlayerAction.Kind))
                throw new InvalidDataException(
                    "Malformed ReferenceNature event-choice player step.");
            return;
        }
        throw new InvalidDataException(
            "Unknown ReferenceNature event-choice session step.");
    }

    public ReferenceNatureControlledEventChoiceStepV111 Fork() => this with
    {
        PlayerAction = PlayerAction is null
            ? null : CloneAction(PlayerAction)
    };

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

/// <summary>
/// Nature cursors and the single ordered event-choice chronology. The steps
/// include both player actions and Nature samples, so the typed Nature
/// transcript is a projection rather than a second stored history.
/// </summary>
public sealed record ReferenceNatureControlledEventChoiceStochasticStateV111
{
    private ReferenceNatureControlledEventChoiceStepV111[]? _steps;

    public ReferenceNatureStateV111 InitialNatureState { get; init; }
    public ReferenceNatureStateV111 NatureState { get; init; }

    public ReferenceNatureControlledEventChoiceStepV111[] Steps
    {
        get => Copy(_steps)!;
        init => _steps = Copy(value);
    }

    [JsonIgnore]
    public ReferenceNatureDecisionV111[] DecisionTranscript => Steps
        .Where(step => step.Kind == "nature")
        .Select(step => step.NatureDecision!)
        .ToArray();

    public ReferenceNatureControlledEventChoiceStochasticStateV111(
        ReferenceNatureStateV111 initialNatureState,
        ReferenceNatureStateV111 natureState,
        ReferenceNatureControlledEventChoiceStepV111[] steps)
    {
        InitialNatureState = initialNatureState ??
            throw new ArgumentNullException(nameof(initialNatureState));
        NatureState = natureState ??
            throw new ArgumentNullException(nameof(natureState));
        Steps = steps;
    }

    public ReferenceNatureControlledEventChoiceStochasticStateV111 Fork() =>
        this with
        {
            InitialNatureState = InitialNatureState.Fork(),
            NatureState = NatureState.Fork(),
            Steps = Steps.Select(step => step.Fork()).ToArray()
        };

    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(InitialNatureState);
        ArgumentNullException.ThrowIfNull(NatureState);
        var steps = Steps;
        ArgumentNullException.ThrowIfNull(steps);
        InitialNatureState.Validate();
        NatureState.Validate();
        foreach (var step in steps)
        {
            ArgumentNullException.ThrowIfNull(step);
            step.Validate();
        }
    }

    private static ReferenceNatureControlledEventChoiceStepV111[]? Copy(
        ReferenceNatureControlledEventChoiceStepV111[]? steps) => steps is null
            ? null
            : steps.Select(step => step?.Fork()!).ToArray();
}

public sealed record ReferenceNatureControlledEventChoiceSnapshotV111(
    string SchemaId, string OriginSnapshotJson, string ChoiceId,
    string RunSnapshotJson, ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureControlledEventChoiceStepV111[] Steps)
{
    private ReferenceNatureControlledEventChoiceStepV111[]? _steps =
        Copy(Steps);

    public ReferenceNatureControlledEventChoiceStepV111[] Steps
    {
        get => Copy(_steps)!;
        init => _steps = Copy(value);
    }

    private static ReferenceNatureControlledEventChoiceStepV111[]? Copy(
        ReferenceNatureControlledEventChoiceStepV111[]? steps) => steps is null
        ? null
        : steps.Select(step => step.Fork()).ToArray();
}

/// <summary>
/// Atomic ReferenceNature envelope for controlled event-effect and
/// post-reward event-choice continuations. Event player actions and Nature
/// samples share one ordered replay transcript and sampler state.
/// </summary>
public sealed record ReferenceNatureControlledEventChoiceSessionV111(
    string SchemaId, RunState Origin, string ChoiceId, RunState Run,
    ReferenceNatureStateV111 InitialNatureState,
    ReferenceNatureStateV111 NatureState,
    ReferenceNatureControlledEventChoiceStepV111[] Steps)
{
    [JsonIgnore]
    public SimulationStateV111<PrototypeMechanicsContinuationStateV111,
        ReferenceNatureControlledEventChoiceStochasticStateV111>
        Simulation { get; init; } = new(
            new PrototypeMechanicsContinuationStateV111(
                PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession,
                Origin, Run),
            new ReferenceNatureControlledEventChoiceStochasticStateV111(
                InitialNatureState, NatureState, Steps));

    [JsonIgnore]
    public PrototypeMechanicsContinuationStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureControlledEventChoiceStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Origin
    {
        get => Mechanics.Origin!;
        init => Mechanics = Mechanics with { Origin = value };
    }

    public RunState Run
    {
        get => Mechanics.Current;
        init => Mechanics = Mechanics with { Current = value };
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

    public ReferenceNatureControlledEventChoiceStepV111[] Steps
    {
        get => Stochastic.Steps;
        init => Stochastic = Stochastic with { Steps = value };
    }

    public const string CurrentSchemaId =
        "sts2-v111-reference-nature-event-choice-session-v1";

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    [JsonIgnore]
    public bool NaturePending =>
        Run.World?.Event?.Stochastic.ControlledChoice is not null ||
        Run.Stochastic.ControlledEventRewardChoice is not null;

    [JsonIgnore]
    public bool CanAdvancePlayer => !NaturePending &&
        (ControlledEventChoiceEngineV111
            .IsStonePushDeckChoiceBoundary(Run)
        || Run.Phase == RunPhase.Reward
            && Run.World?.Event is { } eventState
            && eventState.ChosenChoiceId == ChoiceId
            && Run.World.Reward is
            {
                SourceRoom: var sourceRoom
            }
            && sourceRoom == "Event:" + eventState.EventId
            && Run.Stochastic.ControlledEventChoiceHistory is
                { Length: > 0 } history
            && history[^1].EventId == eventState.EventId
            && history[^1].ChoiceId == ChoiceId);

    [JsonIgnore]
    public bool Complete => !NaturePending && !CanAdvancePlayer;

    [JsonIgnore]
    public ReferenceNatureDecisionV111[] DecisionTranscript =>
        Stochastic.DecisionTranscript;

    public static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId, ulong seed) =>
        Begin(source, choiceId,
            ReferenceNatureRunCursorV111.Begin(
                source, seed, "Event-choice"));

    public static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId) =>
        Begin(source, choiceId,
            ReferenceNatureRunCursorV111.Begin(
                source, null, "Event-choice"));

    internal static ReferenceNatureControlledEventChoiceSessionV111
        BeginForCapture(RunState source, string choiceId,
            ReferenceNatureStateV111 initialNatureState) =>
        Begin(source, choiceId, initialNatureState);

    private static ReferenceNatureControlledEventChoiceSessionV111 Begin(
        RunState source, string choiceId,
        ReferenceNatureStateV111 initialNatureState)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(choiceId);
        ArgumentNullException.ThrowIfNull(initialNatureState);
        ReferenceNatureRunCursorV111.ValidateInitial(
            source, initialNatureState, "Event-choice");
        var origin = NeedsNatureCallingBellBag(source, choiceId)
            ? ReferenceNatureRunProjectionV111.CreateNatureReplayOrigin(
                source, createRelicBagWhenMissing: true)
            : source.Fork();
        var run = ControlledEventChoiceEngineV111.Begin(
            origin.Fork(), choiceId);
        var session = new ReferenceNatureControlledEventChoiceSessionV111(
            CurrentSchemaId, origin, choiceId, run,
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

    private static bool NeedsNatureCallingBellBag(
        RunState source, string choiceId)
    {
        if (source.Stochastic.NativeRunRng is null ||
            source.World is not { } world || world.Event is not
                { EventId: NativeDarvEventV111.EventId } eventState ||
            !PrototypeGameEngine.UsesNativeRegionalSystems(world) ||
            !PrototypeContent.Events.TryGetValue(
                eventState.EventId, out var definition))
            return false;
        return definition.Choices.FirstOrDefault(choice =>
                choice.Id == choiceId)?.Effects.Any(effect =>
                effect.Kind == PrototypeRunEffectKind.GainRelic &&
                effect.RelicId == NativeDarvEventV111.CallingBellId)
            == true;
    }

    public ReferenceNatureControlledEventChoiceSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Mechanics = Mechanics.Fork(),
            Stochastic = Stochastic.Fork()
        };
    }

    public ReferenceNatureControlledEventChoiceSessionV111 ResolveNext()
    {
        Validate();
        if (!NaturePending)
            throw new InvalidOperationException(
                "No controlled event Nature outcome is pending.");
        var law = ChanceLawEngineV111.QueryControlledEventChoice(Run);
        var selection = ReferenceNatureV111.SampleValidated(law, NatureState);
        var resolvedRun = ControlledEventChoiceEngineV111.Resolve(
            Run.Fork(), selection.OutcomeIndex);
        var next = this with
        {
            Run = resolvedRun,
            NatureState = selection.NextState,
            Steps = Steps.Append(new ReferenceNatureControlledEventChoiceStepV111(
                "nature", new ReferenceNatureDecisionV111(
                    law.LawId, law.EventId, selection.OutcomeId,
                    selection.OutcomeIndex))).ToArray()
        };
        if (next.Complete)
            next = next with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    next.Run, selection.NextState)
            };
        next.Validate();
        return next;
    }

    public ReferenceNatureControlledEventChoiceSessionV111 AdvancePlayerAction(
        GameAction action)
    {
        Validate();
        ArgumentNullException.ThrowIfNull(action);
        if (!CanAdvancePlayer)
            throw new InvalidOperationException(
                "No supported event player action is pending.");
        var engine = new PrototypeGameEngine();
        var actionId = PrototypeAiEnvironment.StableActionId(action);
        if (!engine.GetLegalActions(Run).Any(candidate =>
                StringComparer.Ordinal.Equals(
                    PrototypeAiEnvironment.StableActionId(candidate), actionId)))
            throw new InvalidOperationException(
                "ReferenceNature event advance requires a current legal player action.");
        var nextRun = ControlledEventChoiceEngineV111.Advance(
            Run.Fork(), CloneAction(action));
        var next = this with
        {
            Run = nextRun,
            Steps = Steps.Append(new ReferenceNatureControlledEventChoiceStepV111(
                "player_action", PlayerAction: CloneAction(action))).ToArray()
        };
        if (next.Complete)
            next = next with
            {
                Run = ReferenceNatureRunCursorV111.Commit(
                    next.Run, NatureState)
            };
        next.Validate();
        return next;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Origin is null || Run is null ||
            string.IsNullOrWhiteSpace(ChoiceId) || InitialNatureState is null ||
            NatureState is null || Steps is null ||
            Origin.Phase != RunPhase.Event || Origin.World?.Event is not { } originEvent ||
            !PrototypeGameEngine.IsControlledEventChoice(
                originEvent.EventId, ChoiceId))
            throw new InvalidDataException(
                "Invalid ReferenceNature controlled event-choice envelope.");
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ReferenceNatureRunSession);
        Stochastic.Validate();
        PrototypeStateInvariants.Validate(Origin);
        PrototypeStateInvariants.Validate(Run);
        if (NeedsNatureCallingBellBag(Origin, ChoiceId))
        {
            ReferenceNatureRunProjectionV111
                .ValidateNatureRelicBagProjection(
                    Origin, "Native Calling Bell event-choice origin");
            ReferenceNatureRunProjectionV111
                .ValidateNatureRelicBagProjection(
                    Run, "Native Calling Bell event-choice replay");
        }
        var decisions = DecisionTranscript;
        if (!ReferenceNatureRunCursorV111.MatchesRunSession(
                Origin, Run, InitialNatureState, NatureState,
                decisions.Length, Complete))
            throw new InvalidDataException(
                "ReferenceNature event-choice cursor differs from the run envelope.");
        var effect = Run.World?.Event?.Stochastic.ControlledChoice;
        var reward = Run.Stochastic.ControlledEventRewardChoice;
        if (effect is not null && reward is not null)
            throw new InvalidDataException(
                "Run state contains two simultaneous controlled event requests.");
        if (effect is null && reward is null)
        {
            if (ControlledEventChoiceEngineV111
                    .IsStonePushDeckChoiceBoundary(Run))
            {
                if (decisions.Length != 0 || Steps.Length != 0)
                    throw new InvalidDataException(
                        "Stone of All Time push deck-choice boundary has an unexpected transcript prefix.");
                return;
            }
            if (ControlledEventChoiceEngineV111
                    .IsStonePushNoChanceTerminal(Run, Origin, ChoiceId))
            {
                if (decisions.Length != 0 || Steps.Length != 0)
                    throw new InvalidDataException(
                        "Lethal Stone of All Time push has an unexpected chance transcript.");
                return;
            }
            if (Run.Stochastic.ControlledEventChoiceHistory is
                    not { Length: > 0 } history ||
                history[^1].EventId != originEvent.EventId ||
                history[^1].ChoiceId != ChoiceId ||
                history[^1].Decisions.Length != decisions.Length)
                throw new InvalidDataException(
                    "Completed ReferenceNature event choice differs from its mechanics trace.");
            history[^1].Validate();
            for (int index = 0; index < decisions.Length; index++)
                if (history[^1].Decisions[index].SelectedIndex !=
                    decisions[index].OutcomeIndex)
                    throw new InvalidDataException(
                        "Completed event-choice outcomes differ from ReferenceNature history.");
            return;
        }

        var continuationDecisions = effect?.Decisions ?? reward!.Decisions;
        if (continuationDecisions.Length != decisions.Length ||
            (effect?.EventId ?? reward!.EventId) != originEvent.EventId ||
            (effect?.ChoiceId ?? reward!.ChoiceId) != ChoiceId)
            throw new InvalidDataException(
                "Pending event continuation differs from ReferenceNature history.");
        for (int index = 0; index < decisions.Length; index++)
            if (continuationDecisions[index].ProviderMode != "controlled-v111" ||
                continuationDecisions[index].SelectedIndex != decisions[index].OutcomeIndex)
                throw new InvalidDataException(
                    "Pending event outcomes differ from ReferenceNature history.");
    }

    public static string Save(
        ReferenceNatureControlledEventChoiceSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureControlledEventChoiceSnapshotV111(
            CurrentSchemaId, RunSnapshot.Save(session.Origin), session.ChoiceId,
            RunSnapshot.Save(session.Run), session.InitialNatureState,
            session.NatureState,
            session.Steps.Select(step => step.Fork()).ToArray());
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureControlledEventChoiceSessionV111 Restore(
        string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<
                ReferenceNatureControlledEventChoiceSnapshotV111>(json)
            ?? throw new InvalidDataException(
                "Null ReferenceNature event-choice snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.OriginSnapshotJson) ||
            string.IsNullOrWhiteSpace(snapshot.ChoiceId) ||
            string.IsNullOrWhiteSpace(snapshot.RunSnapshotJson) ||
            snapshot.InitialNatureState is null ||
            snapshot.NatureState is null || snapshot.Steps is null)
            throw new InvalidDataException(
                "Invalid ReferenceNature event-choice snapshot.");
        var origin = RunSnapshot.Load(snapshot.OriginSnapshotJson);
        var savedRun = RunSnapshot.Load(snapshot.RunSnapshotJson);
        var replay = Begin(origin, snapshot.ChoiceId,
            snapshot.InitialNatureState);
        foreach (var savedStep in snapshot.Steps)
        {
            savedStep.Validate();
            replay = savedStep.Kind switch
            {
                "nature" when replay.NaturePending => replay.ResolveNext(),
                "player_action" when replay.CanAdvancePlayer =>
                    replay.AdvancePlayerAction(savedStep.PlayerAction!),
                _ => throw new InvalidDataException(
                    "ReferenceNature event-choice replay step is out of order.")
            };
            if (CanonicalJson.Sha256(replay.Steps[^1]) !=
                CanonicalJson.Sha256(savedStep))
                throw new InvalidDataException(
                    "ReferenceNature event-choice replay diverged from its transcript.");
        }
        if (replay.Complete &&
            savedRun.Stochastic.ReferenceNature is null)
            savedRun = ReferenceNatureRunCursorV111.Commit(
                savedRun, snapshot.NatureState);
        if (CanonicalJson.Sha256(replay.Run) != CanonicalJson.Sha256(savedRun) ||
            CanonicalJson.Sha256(replay.NatureState) !=
                CanonicalJson.Sha256(snapshot.NatureState) ||
            CanonicalJson.Sha256(replay.Steps) !=
                CanonicalJson.Sha256(snapshot.Steps))
            throw new InvalidDataException(
                "ReferenceNature event-choice replay differs from its snapshot.");
        replay.Validate();
        return replay;
    }

    private static GameAction CloneAction(GameAction action) => action with
    {
        Payload = action.Payload.Clone()
    };
}

