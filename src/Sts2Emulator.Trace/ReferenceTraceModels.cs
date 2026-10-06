using System.Text.Json;

namespace Sts2Emulator.Trace;

public sealed record ReferenceBuildFile(
    string LogicalName,
    string FileName,
    long SizeBytes,
    string Sha256);

public sealed record ReferenceBuildIdentity(
    string SchemaId,
    JsonElement ReleaseInfo,
    string TargetFramework,
    ReferenceBuildFile[] Files);

public sealed record ReferenceBuildManifest(
    string SchemaId,
    DateTimeOffset CapturedAtUtc,
    string GameDirectory,
    string DataDirectory,
    ReferenceBuildIdentity Identity,
    string BuildFingerprint);

public sealed record ReferenceNormalizedAction(
    string Kind,
    JsonElement Payload);

public sealed record ReferenceRngSnapshot(
    string StreamId,
    string CodecId,
    string Fingerprint,
    JsonElement? State = null,
    long? CallCount = null);

public sealed record ReferenceDecisionSnapshot(
    long DecisionIndex,
    string BoundaryKind,
    JsonElement State,
    string StateHash,
    ReferenceNormalizedAction[] LegalActions,
    ReferenceRngSnapshot[] Rng);

public sealed record ReferenceTraceHeaderV02(
    string Type,
    string TraceSchema,
    ReferenceBuildIdentity Build,
    string BuildFingerprint,
    string BridgeVersion,
    string RunId,
    string RunSeed,
    DateTimeOffset CapturedAtUtc);

public sealed record ReferenceTraceTransitionV02(
    string Type,
    long DecisionIndex,
    string BoundaryKind,
    ReferenceNormalizedAction Action,
    ReferenceDecisionSnapshot Before,
    ReferenceDecisionSnapshot After,
    string[] Diagnostics);
