using Sts2Emulator.Core;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Trace;

public sealed record TraceHeader(
    string Type,
    string TraceSchema,
    string GameBuild,
    string BridgeVersion,
    string? EmulatorVersion,
    string RunId,
    string RunSeed,
    DateTimeOffset CapturedAtUtc);

public sealed record StateEnvelope(
    string Hash,
    RunState State);

public sealed record TraceTransition(
    string Type,
    long DecisionIndex,
    RunPhase Phase,
    GameAction Action,
    StateEnvelope Before,
    StateEnvelope After,
    RngBundle RngBefore,
    RngBundle RngAfter,
    string[] Diagnostics,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    GameAction[]? LegalActionsBefore = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    GameAction[]? LegalActionsAfter = null);
