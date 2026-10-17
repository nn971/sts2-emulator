using System.Threading;

namespace Sts2Emulator.Core;

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

    public static bool IsEnemyActionSite(string siteId) => siteId is
        "combat.enemy.discard.random_insert_index" or
        "combat.enemy.draw.random_insert_index" or
        "combat.enemy.theft_card.select" or
        "combat.enemy.summon_pool.select" or
        NativeCombatRngEntryV111.InitialUniqueHpSiteId or
        NativeCombatRngEntryV111.ToughEggHatchHpSiteId;
}

internal sealed class ControlledEnemyActionChanceRequiredException(
    RandomIndexRequestV111 request) : ControlledChanceRequiredExceptionV111(
        request, null,
        "A controlled enemy action needs a random-choice outcome.");

/// <summary>
/// Injects previously selected enemy-action outcomes while replaying the
/// source resolver from its saved pre-step state. The first unrecorded request
/// suspends the pipeline with its complete typed support.
/// </summary>
internal sealed class ControlledEnemyActionReplayV111 :
    IControlledChanceReplayScopeV111
{
    private static readonly AsyncLocal<ControlledEnemyActionReplayV111?> Active =
        new();
    private readonly ControlledEnemyActionReplayV111? _previous;
    private readonly ControlledRandomIndexReplayPrefixV111 _prefix;
    private bool _disposed;

    public static bool IsActive => Active.Value is not null;

    private ControlledEnemyActionReplayV111(
        RandomIndexDecisionV111[] decisions)
    {
        _previous = Active.Value;
        _prefix = new ControlledRandomIndexReplayPrefixV111(decisions);
        Active.Value = this;
    }

    public static ControlledEnemyActionReplayV111 Begin(
        RandomIndexDecisionV111[] decisions)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        return new ControlledEnemyActionReplayV111(decisions);
    }

    public static int Resolve(RandomIndexRequestV111 request)
    {
        var active = Active.Value ?? throw new InvalidOperationException(
            "Enemy-action replay is not active.");
        request.Validate();
        if (!PrototypeControlledEnemyActionContinuationV111
                .IsEnemyActionSite(request.SiteId))
            throw new NotSupportedException(
                "UNSUPPORTED_CONTROLLED_CHANCE site=" + request.SiteId +
                " operation=" + request.OperationKind);
        if (!active._prefix.TryConsume(
                request,
                "Enemy-action replay diverged from its saved chance request.",
                out var selectedIndex))
            throw new ControlledEnemyActionChanceRequiredException(
                request with
                {
                    OrderedCandidates =
                        (int[])request.OrderedCandidates.Clone()
                });
        return selectedIndex;
    }

    public void EnsureConsumed()
    {
        _prefix.EnsureConsumed(
            "Enemy-action replay left saved chance decisions unused.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (Active.Value != this)
            throw new InvalidOperationException(
                "Enemy-action replay scopes must be disposed in reverse order.");
        Active.Value = _previous;
        _disposed = true;
    }
}

internal sealed class ControlledEnemyActionRandomIndexProviderV111
    : IRandomIndexProviderV111
{
    public string Mode => "controlled-v111";

    public RandomIndexDecisionV111 Resolve(
        RandomIndexRequestV111 request, RngBundle streams)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(streams);
        if (!ControlledChanceGuardV111.Active
            || !ControlledEnemyActionReplayV111.IsActive)
            throw new InvalidOperationException(
                "Controlled enemy-action provider requires an active replay frame.");
        var decision = new RandomIndexDecisionV111(
            request with
            {
                OrderedCandidates = (int[])request.OrderedCandidates.Clone()
            },
            ControlledEnemyActionReplayV111.Resolve(request), Mode);
        decision.Validate();
        return decision;
    }
}
