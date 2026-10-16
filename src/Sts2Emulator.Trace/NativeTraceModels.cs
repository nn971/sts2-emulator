using System.Text.Json;
using Sts2Emulator.Core;

namespace Sts2Emulator.Trace;

/// <summary>Version 0.2 extends the legacy 0.1 JSONL envelope.</summary>
public static class NativeTraceVersion
{
    public const string Current = "0.2";
}

public sealed record CaptureCoverage(
    string[] VerifiedFields,
    string[] MissingFields,
    string[] UnsupportedMechanics,
    string RngStatus,
    string[] VerifiedEvents,
    string[]? RequiredPreconditions = null,
    bool LegalActionsComplete = false);

public sealed record NativeTraceHeader(
    string Type, string TraceSchema, string GameBuild, string BridgeVersion,
    string? EmulatorVersion, string RunId, string RunSeed,
    DateTimeOffset CapturedAtUtc, string CaptureMode, string Provenance);

public sealed record NativeDecisionRecord(
    string Type, long DecisionId, string Phase, JsonElement State,
    string StateHash, GameAction[] LegalActions, string ObservationHash,
    long ObservationRevision, CaptureCoverage Coverage, long EventSeq);

public sealed record NativeActionRecord(
    string Type, long DecisionId, GameAction Action, string Source,
    bool Accepted, string? Reason);

public sealed record NativeEventRecord(
    string Type, long EventSeq, long DecisionId, string Kind,
    string? ActorId, string? TargetId, JsonElement Payload,
    string Origin, string Visibility, long? CausalParent);

public sealed record NativeTransitionRecord(
    string Type, long DecisionId, string BeforeHash, string? AfterHash,
    long FirstEventSeq, long LastEventSeq, string Status);

public sealed record NativeTerminalRecord(
    string Type, long? DecisionId, string Status, string Reason, JsonElement? State = null);
