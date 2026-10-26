using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

public sealed record RunSnapshotEnvelope(
    string Schema, string GameBuild, string StateSchema,
    string StateHash, RunState State);

public sealed record RunSnapshotSimulationEnvelope(
    string Schema, string GameBuild, string StateSchema,
    string StateHash,
    SimulationStateV111<RunMechanicsStateV111,
        RunStochasticStateV111> State);

/// <summary>Exact snapshots contain hidden state and must not be used as fair observations.</summary>
public static class RunSnapshot
{
    public const string SchemaId = "sts2-exact-snapshot-v1";
    public const string ReferenceNatureSchemaId =
        "sts2-exact-snapshot-v2-reference-nature-state";
    public const string NatureRelicBagOrderSchemaId =
        "sts2-exact-snapshot-v3-nature-relic-bag-order";
    public const string SimulationEnvelopeSchemaId =
        "sts2-exact-snapshot-v4-simulation-envelope";
    public const string CurrentSchemaId = SimulationEnvelopeSchemaId;
    public static string[] SupportedSchemaIds =>
    [
        SchemaId,
        ReferenceNatureSchemaId,
        NatureRelicBagOrderSchemaId,
        SimulationEnvelopeSchemaId
    ];
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static string Save(RunState state)
    {
        ValidateSupportedState(state);
        var owned = state.Fork();
        var simulation = owned.Simulation;
        return CanonicalJson.Serialize(new RunSnapshotSimulationEnvelope(
            SimulationEnvelopeSchemaId,
            owned.GameBuild, owned.EmulatorSchema,
            CanonicalJson.Sha256(simulation), simulation));
    }

    public static RunState Load(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using (var document = JsonDocument.Parse(json))
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException(
                    "Snapshot envelope must be a JSON object.");
            if (document.RootElement.TryGetProperty("schema", out var schema))
            {
                if (schema.ValueKind != JsonValueKind.String)
                    throw new InvalidDataException(
                        "Snapshot schema identifier must be a string.");
                var schemaId = schema.GetString();
                if (schemaId == SimulationEnvelopeSchemaId)
                    return LoadSimulationEnvelope(json);
                if (schemaId is not (SchemaId or ReferenceNatureSchemaId or
                        NatureRelicBagOrderSchemaId))
                    throw new InvalidDataException(
                        "Snapshot schema/build does not match its state.");
            }
        }

        var envelope = JsonSerializer.Deserialize<RunSnapshotEnvelope>(json, Options)
            ?? throw new InvalidDataException("Snapshot is empty.");
        if (envelope.Schema is not (SchemaId or ReferenceNatureSchemaId or
                NatureRelicBagOrderSchemaId) ||
            envelope.State is null
            || envelope.GameBuild != envelope.State.GameBuild
            || envelope.StateSchema != envelope.State.EmulatorSchema)
            throw new InvalidDataException("Snapshot schema/build does not match its state.");
        var hasNatureRelicBag = envelope.State.World?.Stochastic.RelicBag?
            .OrderMode ==
            PrototypeRelicBagOrderMode.NatureUnordered;
        if ((envelope.Schema == SchemaId &&
             envelope.State.Stochastic.ReferenceNature is not null) ||
            (envelope.Schema == ReferenceNatureSchemaId &&
             envelope.State.Stochastic.ReferenceNature is null) ||
            (envelope.Schema == NatureRelicBagOrderSchemaId &&
             !hasNatureRelicBag))
            throw new InvalidDataException(
                "Snapshot schema does not match its stochastic provider state.");
        ValidateSupportedState(envelope.State);
        if (!StringComparer.Ordinal.Equals(envelope.StateHash, CanonicalJson.Sha256(envelope.State)))
            throw new InvalidDataException("Snapshot canonical hash does not match its state.");
        return envelope.State.Fork();
    }

    private static RunState LoadSimulationEnvelope(string json)
    {
        var envelope = JsonSerializer.Deserialize<RunSnapshotSimulationEnvelope>(
            json, Options) ?? throw new InvalidDataException(
                "Snapshot is empty.");
        var simulation = envelope.State ?? throw new InvalidDataException(
            "Snapshot is missing its simulation state.");
        var mechanics = simulation.Mechanics;
        if (envelope.Schema != CurrentSchemaId ||
            envelope.GameBuild != mechanics.GameBuild ||
            envelope.StateSchema != mechanics.EmulatorSchema)
            throw new InvalidDataException(
                "Snapshot schema/build does not match its simulation state.");
        if (!StringComparer.Ordinal.Equals(
                envelope.StateHash, CanonicalJson.Sha256(simulation)))
            throw new InvalidDataException(
                "Snapshot canonical hash does not match its simulation state.");

        var stochastic = simulation.Stochastic;
        var state = new RunState(
            mechanics.GameBuild,
            mechanics.EmulatorSchema,
            mechanics.RunId,
            mechanics.RunSeed,
            mechanics.DecisionIndex,
            mechanics.Phase,
            mechanics.Player,
            stochastic.PrototypeRng,
            mechanics.ExtensionState,
            mechanics.World,
            mechanics.Ascension,
            mechanics.Configuration,
            stochastic.NativeRunRng,
            stochastic.ControlledEventChoiceHistory,
            stochastic.ControlledEventRewardChoice,
            stochastic.ControlledRunActionHistory,
            stochastic.ControlledRunActionChoice,
            stochastic.ReferenceNature)
        {
            Simulation = simulation
        };
        ValidateSupportedState(state);
        return state.Fork();
    }

    private static void ValidateSupportedState(RunState state)
    {
        if (state.Configuration is null && (state.GameBuild != "prototype-unbound"
            || state.EmulatorSchema != "prototype-0.1"))
            throw new NotSupportedException("Snapshot state requires a supported, explicit engine/build identity.");
        RunConfiguration.ValidateState(state);
        PrototypeStateInvariants.Validate(state);
        state.Stochastic.ReferenceNatureRunInitializationTranscript?.Validate(
            replayMapGeneration: true);
        if (!state.Stochastic.ReferenceNatureRunInitializationPending &&
            state.Configuration?.UsesExperimentalSilentThreeActRoute == true &&
            state.World?.Act == 1 &&
            state.Stochastic.ReferenceNatureRunInitializationTranscript is
                { } transcript)
            ValidateExperimentalLaterActPreparation(state, transcript);
    }

    private static void ValidateExperimentalLaterActPreparation(
        RunState state,
        ReferenceNatureRunInitializationTranscriptV111 transcript)
    {
        var initialPrototypeRng =
            transcript.LaterActPreparationInitialPrototypeRng;
        if (initialPrototypeRng is null)
            return;
        var mapAudit = transcript.ActOneMapGeneration;
        if ((state.Configuration!.Acts[0] == ActIdentity.Overgrowth) !=
            (mapAudit is not null))
            throw new InvalidDataException(
                "Experimental run-start map audit differs from its configured Act 1 region.");
        var mapDecisionCount = mapAudit?.DecisionTranscript.Length ?? 0;
        var preparationStartIndex = checked(1 + mapDecisionCount);
        var decisions = transcript.DecisionTranscript;
        var laws = transcript.ChanceLaws;
        if (decisions.Length < preparationStartIndex ||
            laws.Length != decisions.Length)
            throw new InvalidDataException(
                "Experimental run-start transcript omits its later-act preparation prefix.");

        var natureState = mapAudit?.NatureState ??
            ReferenceNatureV111.SampleValidated(
                laws[0], transcript.InitialNatureState).NextState;
        var preparation = ReferenceNatureLaterActPreparationSessionV111.Begin(
            initialPrototypeRng, state.Ascension,
            state.Configuration!.Profile,
            natureState);
        var preparationDecisions = decisions
            .Skip(preparationStartIndex).ToArray();
        for (var index = 0; index < preparationDecisions.Length; index++)
        {
            var expected = preparationDecisions[index];
            if (preparation.Complete)
                throw new InvalidDataException(
                    "Experimental run-start preparation ended before its transcript.");
            preparation = preparation.ResolveNext();
            if (CanonicalJson.Sha256(preparation.DecisionTranscript[^1]) !=
                CanonicalJson.Sha256(expected))
                throw new InvalidDataException(
                    "Experimental run-start preparation differs from its generator replay.");

            var savedLaw = laws[preparationStartIndex + index];
            var replayedLaw = preparation.ChanceLaws[^1];
            if (CanonicalJson.Sha256(savedLaw with { OutcomeLabels = null }) !=
                    CanonicalJson.Sha256(replayedLaw with
                    {
                        OutcomeLabels = null
                    }) ||
                savedLaw.OutcomeLabels is { } savedLabels &&
                    !savedLabels.SequenceEqual(
                        replayedLaw.OutcomeLabels ?? []))
                throw new InvalidDataException(
                    "Experimental run-start Chance Law differs from its generator replay.");
        }

        var world = state.World ?? throw new InvalidDataException(
            "Experimental run-start snapshot lost its world.");
        if (!preparation.Complete ||
            !preparation.NatureState.SameCursor(transcript.FinalNatureState) ||
            CanonicalJson.Sha256(preparation.Bags) != CanonicalJson.Sha256(
                world.Stochastic.PreparedLaterActEncounterBags))
            throw new InvalidDataException(
                "Experimental run-start bags or Nature cursor differ from generator replay.");
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
