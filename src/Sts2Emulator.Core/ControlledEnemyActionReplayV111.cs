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
    public PrototypeControlledEnemyActionContinuationV111 Fork() => this with
    {
        Checkpoint = Checkpoint.Fork(),
        Decisions = Decisions.Select(decision => decision with
        {
            Request = decision.Request with
            {
                OrderedCandidates =
                    (int[])decision.Request.OrderedCandidates.Clone()
            }
        }).ToArray(),
        PendingRequest = PendingRequest with
        {
            OrderedCandidates =
                (int[])PendingRequest.OrderedCandidates.Clone()
        }
    };

    public void Validate(int pipelineLength)
    {
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
    RandomIndexRequestV111 request) : Exception(
        "A controlled enemy action needs a random-choice outcome.")
{
    public RandomIndexRequestV111 Request { get; } = request;
}

/// <summary>
/// Injects previously selected enemy-action outcomes while replaying the
/// source resolver from its saved pre-step state. The first unrecorded request
/// suspends the pipeline with its complete typed support.
/// </summary>
internal sealed class ControlledEnemyActionReplayV111 : IDisposable
{
    private static readonly AsyncLocal<ControlledEnemyActionReplayV111?> Active =
        new();
    private readonly ControlledEnemyActionReplayV111? _previous;
    private readonly RandomIndexDecisionV111[] _decisions;
    private int _nextDecision;
    private bool _disposed;

    public static bool IsActive => Active.Value is not null;

    private ControlledEnemyActionReplayV111(
        RandomIndexDecisionV111[] decisions)
    {
        _previous = Active.Value;
        _decisions = decisions;
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
        if (active._nextDecision >= active._decisions.Length)
            throw new ControlledEnemyActionChanceRequiredException(
                request with
                {
                    OrderedCandidates =
                        (int[])request.OrderedCandidates.Clone()
                });

        var decision = active._decisions[active._nextDecision++];
        decision.Validate();
        if (CanonicalJson.Sha256(decision.Request) !=
            CanonicalJson.Sha256(request))
            throw new InvalidDataException(
                "Enemy-action replay diverged from its saved chance request.");
        return decision.SelectedIndex;
    }

    public void EnsureConsumed()
    {
        if (_nextDecision != _decisions.Length)
            throw new InvalidDataException(
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
