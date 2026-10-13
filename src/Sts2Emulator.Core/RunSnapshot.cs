using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record RunSnapshotEnvelope(
    string Schema, string GameBuild, string StateSchema,
    string StateHash, RunState State);

/// <summary>Exact snapshots contain hidden state and must not be used as fair observations.</summary>
public static class RunSnapshot
{
    public const string SchemaId = "sts2-exact-snapshot-v1";
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Save(RunState state)
    {
        ValidateSupportedState(state);
        var owned = state.Fork();
        return CanonicalJson.Serialize(new RunSnapshotEnvelope(
            SchemaId, owned.GameBuild, owned.EmulatorSchema,
            CanonicalJson.Sha256(owned), owned));
    }

    public static RunState Load(string json)
    {
        var envelope = JsonSerializer.Deserialize<RunSnapshotEnvelope>(json, Options)
            ?? throw new InvalidDataException("Snapshot is empty.");
        if (envelope.Schema != SchemaId || envelope.State is null
            || envelope.GameBuild != envelope.State.GameBuild
            || envelope.StateSchema != envelope.State.EmulatorSchema)
            throw new InvalidDataException("Snapshot schema/build does not match its state.");
        ValidateSupportedState(envelope.State);
        if (!StringComparer.Ordinal.Equals(envelope.StateHash, CanonicalJson.Sha256(envelope.State)))
            throw new InvalidDataException("Snapshot canonical hash does not match its state.");
        return envelope.State.Fork();
    }

    private static void ValidateSupportedState(RunState state)
    {
        if (state.Configuration is null && (state.GameBuild != "prototype-unbound"
            || state.EmulatorSchema != "prototype-0.1"))
            throw new NotSupportedException("Snapshot state requires a supported, explicit engine/build identity.");
        RunConfiguration.ValidateState(state);
        PrototypeStateInvariants.Validate(state);
    }
}

public sealed record BatchStepRequest(RunState State, GameAction Action);

public static class DeterministicBatch
{
    public const string SchemaId = "sts2-deterministic-batch-v1";
    public static TransitionResult[] Step(
        IReadOnlyList<BatchStepRequest> requests, int maxDegreeOfParallelism = 1)
    {
        if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
        var results = new TransitionResult[requests.Count];
        Parallel.For(0, requests.Count, new ParallelOptions { MaxDegreeOfParallelism = maxDegreeOfParallelism },
            index => results[index] = new PrototypeGameEngine().Step(requests[index].State, requests[index].Action));
        return results;
    }
}
