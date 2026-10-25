using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>Forkable state for the independent ReferenceNature sampler.</summary>
public sealed record ReferenceNatureStateV111(
    string SchemaId, ulong SamplerState, long SampleCount)
{
    public const string CurrentSchemaId = "sts2-v111-reference-nature-state-v1";

    public static ReferenceNatureStateV111 Create(ulong seed) =>
        new(CurrentSchemaId, seed, 0);

    public ReferenceNatureStateV111 Fork() => this;

    public static ReferenceNatureStateV111 ForRunSession(
        RunState run, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Stochastic.ReferenceNature is { } saved)
        {
            if (seed is not null)
                throw new InvalidOperationException(
                    "This run already owns a ReferenceNature cursor; omit nature_seed to continue it.");
            saved.Validate();
            return saved.Fork();
        }
        if (seed is null)
            throw new InvalidOperationException(
                "A first ReferenceNature session requires nature_seed.");
        return Create(seed.Value);
    }

    public bool SameCursor(ReferenceNatureStateV111? other) =>
        other is not null &&
        CanonicalJson.Sha256(this) == CanonicalJson.Sha256(other);

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || SampleCount < 0)
            throw new InvalidDataException("Invalid ReferenceNature sample count.");
    }
}

/// <summary>
/// Shared lifecycle rules for ReferenceNature sessions attached to a run.
/// Boundary sessions keep their mechanics and transcript types, but cursor
/// initialization, publication, and run-cursor validation have one owner.
/// </summary>
internal static class ReferenceNatureRunCursorV111
{
    public static ReferenceNatureStateV111 Begin(
        RunState source, ulong? seed, string boundary)
    {
        ArgumentNullException.ThrowIfNull(source);
        var cursor = ReferenceNatureStateV111.ForRunSession(source, seed);
        ValidateInitial(source, cursor, boundary);
        return cursor;
    }

    public static void ValidateInitial(
        RunState source,
        ReferenceNatureStateV111 initial,
        string boundary)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(initial);
        if (source.Stochastic.ReferenceNature is { } saved &&
            !initial.SameCursor(saved))
            throw new InvalidOperationException(
                $"{boundary} session cannot replace the run's ReferenceNature cursor.");
    }

    public static bool MatchesRunSession(
        RunState origin,
        RunState current,
        ReferenceNatureStateV111 initial,
        ReferenceNatureStateV111 cursor,
        int decisionCount,
        bool complete)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(cursor);

        var initialMatchesOrigin =
            origin.Stochastic.ReferenceNature is not { } saved ||
            initial.SameCursor(saved);
        var pendingRunMatchesOrigin =
            origin.Stochastic.ReferenceNature is { } originCursor
                ? originCursor.SameCursor(current.Stochastic.ReferenceNature)
                : current.Stochastic.ReferenceNature is null;
        var runMatchesCursor = complete
            ? cursor.SameCursor(current.Stochastic.ReferenceNature)
            : pendingRunMatchesOrigin;
        return initialMatchesOrigin && runMatchesCursor &&
            cursor.SampleCount == checked(initial.SampleCount + decisionCount);
    }

    public static RunState Commit(
        RunState run, ReferenceNatureStateV111 cursor)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(cursor);
        return run with
        {
            Stochastic = RunStochasticStateV111.CommitReferenceNatureCursor(
                run.Stochastic, cursor)
        };
    }
}

public sealed record ReferenceNatureSelectionV111(
    string LawId, string EventId, string OutcomeId, int OutcomeIndex,
    ReferenceNatureStateV111 NextState);

public sealed record ReferenceNatureResolutionV111(
    SemanticPilotCombatV111 Combat, ChanceLawV111 Law,
    ReferenceNatureSelectionV111 Selection);

public sealed record ReferenceNatureSessionSnapshotV111(
    string SchemaId, string CombatSnapshotJson,
    ReferenceNatureStateV111 NatureState)
{
    // Added without changing the established flat snapshot members. Older
    // snapshots begin their transcript at the saved current cursor.
    public ReferenceNatureStateV111? InitialNatureState { get; init; }
    public ReferenceNatureSelectionV111[]? DecisionTranscript { get; init; }
}

/// <summary>
/// Shared typed cursor and selection transcript held by the atomic semantic
/// ReferenceNature session.
/// </summary>
public sealed record ReferenceNatureSessionStochasticStateV111
{
    private ReferenceNatureStochasticStateV111<ReferenceNatureSelectionV111>
        _cursor = null!;

    public ReferenceNatureStochasticStateV111<ReferenceNatureSelectionV111>
        Cursor
    {
        get => _cursor;
        init => _cursor = value ?? throw new ArgumentNullException(nameof(Cursor));
    }

    public ReferenceNatureStateV111 InitialNatureState
    {
        get => Cursor.InitialNatureState;
        init => Cursor = Cursor with { InitialNatureState = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Cursor.NatureState;
        init => Cursor = Cursor with { NatureState = value };
    }

    public ReferenceNatureSelectionV111[] DecisionTranscript
    {
        get => Cursor.DecisionTranscript;
        init => Cursor = Cursor with { DecisionTranscript = value };
    }

    public ReferenceNatureSessionStochasticStateV111(
        ReferenceNatureStateV111 natureState)
        : this(new ReferenceNatureStochasticStateV111<
            ReferenceNatureSelectionV111>(natureState, natureState, []))
    {
    }

    public ReferenceNatureSessionStochasticStateV111(
        ReferenceNatureStochasticStateV111<ReferenceNatureSelectionV111> cursor)
    {
        Cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
    }

    public void Deconstruct(out ReferenceNatureStateV111 NatureState) =>
        NatureState = this.NatureState;

    public ReferenceNatureSessionStochasticStateV111 Fork() => this with
    {
        Cursor = Cursor.Fork()
    };

    public void Validate()
    {
        Cursor.Validate();
        var transcript = DecisionTranscript;
        var expectedSampleCount = checked(
            InitialNatureState.SampleCount + transcript.Length);
        if (NatureState.SampleCount != expectedSampleCount)
            throw new InvalidDataException(
                "ReferenceNature semantic transcript does not match its sample count.");

        var previous = InitialNatureState;
        foreach (var selection in transcript)
        {
            if (selection is null ||
                string.IsNullOrWhiteSpace(selection.LawId) ||
                string.IsNullOrWhiteSpace(selection.EventId) ||
                string.IsNullOrWhiteSpace(selection.OutcomeId) ||
                selection.OutcomeIndex < 0 || selection.NextState is null)
                throw new InvalidDataException(
                    "ReferenceNature semantic transcript contains an invalid selection.");
            selection.NextState.Validate();
            if (selection.NextState.SampleCount != checked(previous.SampleCount + 1))
                throw new InvalidDataException(
                    "ReferenceNature semantic transcript cursor sequence is invalid.");
            previous = selection.NextState;
        }

        if (!previous.SameCursor(NatureState))
            throw new InvalidDataException(
                "ReferenceNature semantic transcript does not end at its current cursor.");
    }
}

/// <summary>
/// Atomic ReferenceNature simulation state: semantic mechanics continuation
/// and the independent sampler cursor share one fork/save/restore boundary.
/// </summary>
public sealed record ReferenceNatureSessionV111(
    string SchemaId, SemanticPilotCombatV111 Combat,
    ReferenceNatureStateV111 NatureState)
{
    public const string CurrentSchemaId = "sts2-v111-reference-nature-session-v1";

    [JsonIgnore]
    public SimulationStateV111<SemanticPilotCombatV111,
        ReferenceNatureSessionStochasticStateV111> Simulation { get; init; } =
        new(Combat, new ReferenceNatureSessionStochasticStateV111(NatureState));

    public SemanticPilotCombatV111 Combat
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    [JsonIgnore]
    public ReferenceNatureSessionStochasticStateV111 Stochastic
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public ReferenceNatureStateV111 NatureState
    {
        get => Stochastic.NatureState;
        init => Stochastic = Stochastic with { NatureState = value };
    }

    [JsonIgnore]
    public string Fingerprint => CanonicalJson.Sha256(this);

    public static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat, ulong seed) => Begin(combat, (ulong?)seed);

    public static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat) => Begin(combat, null);

    private static ReferenceNatureSessionV111 Begin(
        SemanticPilotCombatV111 combat, ulong? seed)
    {
        ArgumentNullException.ThrowIfNull(combat);
        var natureState = ReferenceNatureRunCursorV111.Begin(
            combat.Run, seed, "Semantic combat");
        if (combat.CombatTerminal &&
            !natureState.SameCursor(
                combat.Run.Stochastic.ReferenceNature))
        {
            var committedRun = ReferenceNatureRunCursorV111.Commit(
                combat.Run, natureState);
            combat = combat with
            {
                Run = committedRun,
                PendingEndTurn = combat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var session = new ReferenceNatureSessionV111(
            CurrentSchemaId, combat.Fork(), natureState);
        session.Validate();
        return session;
    }

    public ReferenceNatureSessionV111 Fork()
    {
        Validate();
        return this with
        {
            Simulation = Simulation with
            {
                Mechanics = Combat.Fork(),
                Stochastic = Stochastic.Fork()
            }
        };
    }

    public ReferenceNatureSessionV111 ResolveNext()
    {
        Validate();
        var resolution = ReferenceNatureV111.ResolvePending(Combat, NatureState);
        var resolvedCombat = resolution.Combat;
        if (resolvedCombat.CombatTerminal)
        {
            var committedRun = ReferenceNatureRunCursorV111.Commit(
                resolvedCombat.Run, resolution.Selection.NextState);
            resolvedCombat = resolvedCombat with
            {
                Run = committedRun,
                PendingEndTurn = resolvedCombat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var child = this with
        {
            Combat = resolvedCombat,
            Stochastic = Stochastic with
            {
                Cursor = Stochastic.Cursor.Advance(
                    resolution.Selection.NextState, resolution.Selection)
            }
        };
        child.Validate();
        return child;
    }

    public void Validate()
    {
        if (SchemaId != CurrentSchemaId || Combat is null || Stochastic is null)
            throw new InvalidDataException("Invalid ReferenceNature session envelope.");
        Combat.Validate();
        Stochastic.Validate();
        if (Combat.CombatTerminal &&
            !NatureState.SameCursor(
                Combat.Run.Stochastic.ReferenceNature))
            throw new InvalidDataException(
                "ReferenceNature sampler cursor differs from the committed run state.");
    }

    public static string Save(ReferenceNatureSessionV111 session)
    {
        ArgumentNullException.ThrowIfNull(session);
        session.Validate();
        var snapshot = new ReferenceNatureSessionSnapshotV111(
            CurrentSchemaId,
            SemanticPilotCombatEngineV111.Save(session.Combat),
            session.NatureState)
        {
            InitialNatureState = session.Stochastic.InitialNatureState,
            DecisionTranscript = session.Stochastic.DecisionTranscript
        };
        return JsonSerializer.Serialize(snapshot);
    }

    public static ReferenceNatureSessionV111 Restore(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var snapshot = JsonSerializer.Deserialize<ReferenceNatureSessionSnapshotV111>(json)
            ?? throw new InvalidDataException("Null ReferenceNature snapshot.");
        if (snapshot.SchemaId != CurrentSchemaId ||
            string.IsNullOrWhiteSpace(snapshot.CombatSnapshotJson) ||
            snapshot.NatureState is null)
            throw new InvalidDataException("Invalid ReferenceNature snapshot.");
        var combat = SemanticPilotCombatEngineV111.Restore(
            snapshot.CombatSnapshotJson);
        if (combat.CombatTerminal &&
            combat.Run.Stochastic.ReferenceNature is null)
        {
            var committedRun = ReferenceNatureRunCursorV111.Commit(
                combat.Run, snapshot.NatureState);
            combat = combat with
            {
                Run = committedRun,
                PendingEndTurn = combat.PendingEndTurn is { } endTurn
                    ? endTurn with { Run = committedRun }
                    : null
            };
        }
        var stochastic = new ReferenceNatureSessionStochasticStateV111(
            new ReferenceNatureStochasticStateV111<
                ReferenceNatureSelectionV111>(
                    snapshot.InitialNatureState ?? snapshot.NatureState,
                    snapshot.NatureState,
                    snapshot.DecisionTranscript ?? []));
        var restored = new ReferenceNatureSessionV111(
            CurrentSchemaId, combat, snapshot.NatureState)
        {
            Stochastic = stochastic
        };
        restored.Validate();
        return restored;
    }
}

