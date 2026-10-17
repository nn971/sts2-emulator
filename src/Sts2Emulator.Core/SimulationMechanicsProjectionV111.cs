namespace Sts2Emulator.Core;

/// <summary>
/// Provider-independent projection of one run's committed game mechanics.
/// Nested domain owners are projected through their existing mechanics
/// envelopes; unresolved provider payloads and private stochastic state are
/// omitted.
/// </summary>
public sealed record SimulationMechanicsProjectionV111(
    RunMechanicsStateV111 Run,
    RunWorldMechanicsStateV111? World,
    CombatMechanicsStateV111? Combat,
    EventMechanicsStateV111? Event);

/// <summary>
/// Builds stable provider-independent mechanics projections for replay and
/// cross-provider comparisons. Callers should compare states reached after
/// applying the same semantic outcomes.
/// </summary>
public static class SimulationMechanicsProjectionEngineV111
{
    public static SimulationMechanicsProjectionV111 Project(RunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var world = run.World;
        var combat = world?.Combat?.Mechanics;
        if (combat is not null)
        {
            var pipeline = combat.AutomaticPipelineContinuation;
            if (pipeline is not null)
                pipeline = pipeline with
                {
                    Stochastic = new PrototypeAutomaticPipelineStochasticStateV111()
                };

            combat = combat with
            {
                PendingChoice = WithoutProviderPayload(
                    combat.PendingChoice),
                AutomaticPipelineContinuation = pipeline
            };
        }

        var runMechanics = run.Mechanics with
        {
            // The seed selects provider realizations, not game mechanics.
            RunSeed = string.Empty,
            World = null
        };
        RunWorldMechanicsStateV111? worldMechanics = world is null
            ? null
            : world.Mechanics with
            {
                Combat = null,
                Event = null
            };

        return new SimulationMechanicsProjectionV111(
            runMechanics,
            worldMechanics,
            combat,
            world?.Event?.Mechanics);
    }

    public static string Fingerprint(RunState run) =>
        CanonicalJson.Sha256(Project(run));

    public static bool MechanicallyEquivalent(
        RunState left, RunState right) =>
        StringComparer.Ordinal.Equals(
            Fingerprint(left), Fingerprint(right));

    private static PendingCombatChoiceState? WithoutProviderPayload(
        PendingCombatChoiceState? pending) => pending is null
            ? null
            : pending with
            {
                ControlledTarget = null,
                ControlledGeneration = null,
                ControlledPotionGeneration = null,
                ControlledCardTransform = null,
                ControlledDrawInsertion = null,
                ControlledPoolShuffle = null,
                ControlledRandomCost = null,
                ControlledDrawCost = null,
                ControlledCardShortlist = null,
                ControlledRandomPileCard = null,
                ControlledSemanticDraw = null,
                SemanticStratagemDrawSelection = false,
                AutomaticSemanticStratagemDrawSelection = false,
                StandaloneSemanticDrawHookChoice = false,
                AutomaticSemanticDrawHookChoice = false
            };
}
