namespace Sts2Emulator.Core;

/// <summary>
/// Runs a REAL PrototypeGameEngine.Step in a strict source-shuffle replay
/// context. Shuffle source evidence must already have independently mapped
/// native card object identities to emulator combat instance IDs; source
/// fixture parsing is handled separately. The engine's other mechanics and
/// resulting state remain those computed by Step.
/// </summary>
public static class NativeShuffleStepFidelityV111
{
    public sealed record Report(
        string Status,
        bool ExecutedActualEngineStep,
        string NativeGameParity,
        string ExactNativeRngParity,
        string SourceShuffleStageParity,
        int ExpectedSourceShuffles,
        int ObservedSourceShuffles,
        string? FirstMismatchStage,
        NativeShuffleStepObservationV111[] Observations,
        string? EngineFailure,
        string[] UnverifiedPreconditions,
        RunState? EmulatorAfter = null);

    public static Report Compare(
        RunState before,
        GameAction action,
        IReadOnlyList<NativeShuffleStepExpectationV111> sourceShuffles,
        RngBundle diagnosticNativeStreams)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(sourceShuffles);
        ArgumentNullException.ThrowIfNull(diagnosticNativeStreams);
        if (sourceShuffles.Count == 0)
            return new Report(
                "NO_NATIVE_SOURCE_SHUFFLE_EVIDENCE", false, "NOT_TESTED",
                "NOT_TESTED", "NOT_TESTED", 0, 0, null, [], null,
                ["A source-observed shuffle is required before Step replay."]);

        var guards = new[]
        {
            "PRESTATE_IMPORT_PROVENANCE_REQUIRES_INDEPENDENT_VERIFICATION",
            "NATIVE_CARD_OBJECT_TO_EMULATOR_INSTANCE_MAP_ASSUMED_VERIFIED",
            "NATIVE_RNG_CURSOR_REPLAY_IS_SCRIPTED_NOT_SEEDED",
            "UNMIGRATED_RANDOM_PROVIDER_SITES",
            "NON_SHUFFLE_NATIVE_HOOK_ORDER_NOT_TESTED"
        };

        NativeShuffleStepReplayV111 scope;
        try
        {
            scope = NativeShuffleStepReplayV111.Begin(
                sourceShuffles, diagnosticNativeStreams);
        }
        catch (ArgumentException error)
        {
            return new Report(
                "INVALID_SOURCE_SHUFFLE_EVIDENCE", false, "NOT_TESTED",
                "NOT_TESTED", "NOT_TESTED", sourceShuffles.Count, 0, null,
                [], error.Message, guards);
        }

        using (scope)
        {
            try
            {
                var engine = new PrototypeGameEngine();
                var after = engine.Step(before, action).State;
                var observations = scope.Observations.ToArray();
                if (scope.Remaining > 0 || scope.Status == "REPLAY_INCOMPLETE")
                    return new Report(
                        "SOURCE_SHUFFLE_STEP_REPLAY_INCOMPLETE", true,
                        "NOT_TESTED", "NOT_TESTED", "NOT_TESTED",
                        sourceShuffles.Count, observations.Length,
                        "missing_source_shuffle", observations, null, guards,
                        after);
                var first = observations.FirstOrDefault(o =>
                    o.Status != "SOURCE_SHUFFLE_STAGE_CONSISTENT");
                return new Report(
                    first is null ? "SOURCE_SHUFFLE_STEP_STAGES_CONSISTENT"
                        : "SOURCE_SHUFFLE_STEP_STAGE_MISMATCH",
                    true, "NOT_TESTED", "NOT_TESTED",
                    first is null ? "MATCHED_OBSERVED_SHUFFLE_STAGES"
                        : "MISMATCH",
                    sourceShuffles.Count, observations.Length,
                    first?.FirstMismatchStage,
                    observations, null, guards, after);
            }
            catch (Exception error) when (error is InvalidOperationException
                                          or ArgumentException
                                          or NotSupportedException)
            {
                var observations = scope.Observations.ToArray();
                return new Report(
                    "SOURCE_SHUFFLE_STEP_EXECUTION_BLOCKED",
                    false, "NOT_TESTED", "NOT_TESTED", "NOT_TESTED",
                    sourceShuffles.Count, observations.Length,
                    observations.LastOrDefault()?.FirstMismatchStage,
                    observations, error.GetType().Name + ": " + error.Message,
                    guards);
            }
        }
    }
}
