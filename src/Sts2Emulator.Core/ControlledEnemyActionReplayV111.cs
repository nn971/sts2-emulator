namespace Sts2Emulator.Core;

public enum ControlledEnemyActionMechanicsOperationV111
{
    DrawPileInsertion,
    DiscardPileInsertion,
    TheftCardSelection,
    SummonPoolSelection,
    InitialUniqueHp,
    ToughEggHatchlingHp
}

/// <summary>
/// Replay checkpoint for guarded random choices during the enemy action
/// pipeline. The checkpoint is captured before ResolveEnemyActions and has
/// no automatic cursor of its own, so the saved graph is finite.
/// </summary>
public sealed record PrototypeControlledEnemyActionContinuationV111(
    RunState Checkpoint,
    int StepIndex,
    RandomIndexDecisionV111[] Decisions,
    RandomIndexRequestV111 PendingRequest)
{
    /// <summary>
    /// Atomic mechanics/provider envelope for the suspended enemy action.
    /// Existing checkpoint and chance properties remain flattened snapshot
    /// projections; the pipeline index remains boundary-owned.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SimulationStateV111<PrototypeMechanicsContinuationStateV111,
        ControlledChanceContinuationStateV111> Simulation { get; init; } = new(
            new PrototypeMechanicsContinuationStateV111(
                PrototypeMechanicsContinuationKindV111.ControlledEnemyAction,
                null, Checkpoint),
            new ControlledChanceContinuationStateV111(
                Decisions, PendingRequest));

    [System.Text.Json.Serialization.JsonIgnore]
    public PrototypeMechanicsContinuationStateV111 Mechanics
    {
        get => Simulation.Mechanics;
        init => Simulation = Simulation with { Mechanics = value };
    }

    /// <summary>
    /// Shared provider payload; the legacy continuation fields below remain
    /// its flattened snapshot projection.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ControlledChanceContinuationStateV111 Chance
    {
        get => Simulation.Stochastic;
        init => Simulation = Simulation with { Stochastic = value };
    }

    public RunState Checkpoint
    {
        get => Mechanics.Current;
        init => Mechanics = Mechanics with { Current = value };
    }

    public RandomIndexDecisionV111[] Decisions
    {
        get => Chance.Decisions;
        init => Chance = Chance with { Decisions = value };
    }

    public RandomIndexRequestV111 PendingRequest
    {
        get => Chance.PendingRequest;
        init => Chance = Chance with { PendingRequest = value };
    }

    public PrototypeControlledEnemyActionContinuationV111 Fork() => this with
    {
        Checkpoint = Checkpoint.Fork(),
        Chance = Chance.Fork()
    };

    public void Validate(int pipelineLength)
    {
        Mechanics.Validate(
            PrototypeMechanicsContinuationKindV111.ControlledEnemyAction);
        if (Checkpoint is null || Decisions is null || PendingRequest is null ||
            StepIndex < 0 || StepIndex >= pipelineLength ||
            PrototypeContent.Rules.EndTurnPipeline[StepIndex].Kind !=
                PrototypeAutomaticStepKind.ResolveEnemyActions)
            throw new InvalidDataException(
                "Malformed controlled enemy-action checkpoint.");
        PendingRequest.Validate();
        if (!IsEnemyActionSite(PendingRequest.SiteId))
            throw new InvalidDataException(
                "Enemy-action continuation contains an unrelated chance site.");
        foreach (var decision in Decisions)
        {
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null ||
                !IsEnemyActionSite(decision.Request.SiteId))
                throw new InvalidDataException(
                    "Enemy-action replay prefix contains an invalid Nature decision.");
        }

        if (Checkpoint.Phase != RunPhase.Combat ||
            Checkpoint.World?.Combat is not { IsPlayerTurn: false,
                AutomaticPipelineContinuation: null } checkpointCombat)
            throw new InvalidDataException(
                "Enemy-action checkpoint is outside the enemy turn boundary.");
        PrototypeStateInvariants.Validate(Checkpoint);
    }

    public static bool IsEnemyActionSite(string siteId) =>
        TryGetMechanicsOperation(siteId, out _);

    public static bool TryGetMechanicsOperation(
        string siteId,
        out ControlledEnemyActionMechanicsOperationV111 operation)
    {
        switch (siteId)
        {
            case "combat.enemy.draw.random_insert_index":
                operation = ControlledEnemyActionMechanicsOperationV111
                    .DrawPileInsertion;
                return true;
            case "combat.enemy.discard.random_insert_index":
                operation = ControlledEnemyActionMechanicsOperationV111
                    .DiscardPileInsertion;
                return true;
            case StochasticSiteRegistryV111
                .CombatEnemyTheftCardSelectSiteId:
                operation = ControlledEnemyActionMechanicsOperationV111
                    .TheftCardSelection;
                return true;
            case StochasticSiteRegistryV111
                .CombatEnemySummonPoolSelectSiteId:
                operation = ControlledEnemyActionMechanicsOperationV111
                    .SummonPoolSelection;
                return true;
            case NativeCombatRngEntryV111.InitialUniqueHpSiteId:
                operation = ControlledEnemyActionMechanicsOperationV111
                    .InitialUniqueHp;
                return true;
            case NativeCombatRngEntryV111.ToughEggHatchHpSiteId:
                operation = ControlledEnemyActionMechanicsOperationV111
                    .ToughEggHatchlingHp;
                return true;
            default:
                operation = default;
                return false;
        }
    }
}

internal sealed class ControlledEnemyActionChanceRequiredException(
    RandomIndexRequestV111 request) : ControlledChanceRequiredExceptionV111(
        request, null,
        "A controlled enemy action needs a random-choice outcome.");
