using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Provider-private state and provenance owned by the semantic pilot. The
/// public pilot members remain compatibility projections for its current
/// serialized representation.
/// </summary>
public sealed record SemanticPilotStochasticStateV111
{
    private SemanticNativePolicyAuditV111? _nativePolicyAudit;
    private SemanticSourceRouteAuditV111? _sourceRouteAudit;
    private SemanticEndTurnStochasticStateV111? _pendingEndTurn;
    private SemanticSeededEntryV111? _seededEntry;

    [JsonConstructor]
    public SemanticPilotStochasticStateV111(
        SemanticNativePolicyAuditV111? NativePolicyAudit = null,
        SemanticSourceRouteAuditV111? SourceRouteAudit = null,
        SemanticEndTurnStochasticStateV111? PendingEndTurn = null,
        SemanticSeededEntryV111? SeededEntry = null)
    {
        _nativePolicyAudit = CloneNativePolicyAudit(NativePolicyAudit);
        _sourceRouteAudit = CloneSourceRouteAudit(SourceRouteAudit);
        _pendingEndTurn = PendingEndTurn?.Fork();
        _seededEntry = SeededEntry;
    }

    public SemanticNativePolicyAuditV111? NativePolicyAudit
    {
        get => CloneNativePolicyAudit(_nativePolicyAudit);
        init => _nativePolicyAudit = CloneNativePolicyAudit(value);
    }
    public SemanticSourceRouteAuditV111? SourceRouteAudit
    {
        get => CloneSourceRouteAudit(_sourceRouteAudit);
        init => _sourceRouteAudit = CloneSourceRouteAudit(value);
    }
    public SemanticEndTurnStochasticStateV111? PendingEndTurn
    {
        get => _pendingEndTurn?.Fork();
        init => _pendingEndTurn = value?.Fork();
    }
    public SemanticSeededEntryV111? SeededEntry
    {
        get => _seededEntry;
        init => _seededEntry = value;
    }

    public SemanticPilotStochasticStateV111 Fork() => this with
    {
        PendingEndTurn = _pendingEndTurn?.Fork(),
        NativePolicyAudit = CloneNativePolicyAudit(_nativePolicyAudit),
        SourceRouteAudit = CloneSourceRouteAudit(_sourceRouteAudit),
        SeededEntry = _seededEntry
    };

    public void Deconstruct(
        out SemanticNativePolicyAuditV111? NativePolicyAudit,
        out SemanticSourceRouteAuditV111? SourceRouteAudit,
        out SemanticEndTurnStochasticStateV111? PendingEndTurn,
        out SemanticSeededEntryV111? SeededEntry)
    {
        NativePolicyAudit = this.NativePolicyAudit;
        SourceRouteAudit = this.SourceRouteAudit;
        PendingEndTurn = this.PendingEndTurn;
        SeededEntry = this.SeededEntry;
    }

    public void Validate()
    {
        NativePolicyAudit?.Validate();
        PendingEndTurn?.Validate();
    }

    internal static SemanticPilotStochasticStateV111 WithNativePolicyAudit(
        SemanticPilotStochasticStateV111? current,
        SemanticNativePolicyAuditV111? nativePolicyAudit) =>
        (current ?? new SemanticPilotStochasticStateV111()) with
        {
            NativePolicyAudit = nativePolicyAudit
        };

    internal static SemanticPilotStochasticStateV111 WithSourceRouteAudit(
        SemanticPilotStochasticStateV111? current,
        SemanticSourceRouteAuditV111? sourceRouteAudit) =>
        (current ?? new SemanticPilotStochasticStateV111()) with
        {
            SourceRouteAudit = sourceRouteAudit
        };

    private static SemanticNativePolicyAuditV111? CloneNativePolicyAudit(
        SemanticNativePolicyAuditV111? audit) => audit is null ? null :
        audit with
        {
            Decisions = Clone(audit.Decisions, decision => decision with
            {
                Request = decision.Request with
                {
                    OrderedCandidates = (int[])decision.Request
                        .OrderedCandidates.Clone()
                }
            })!,
            ProviderTrace = Clone(audit.ProviderTrace, entry => entry with
            {
                Event = entry.Event.Clone()
            })!
        };

    private static SemanticSourceRouteAuditV111? CloneSourceRouteAudit(
        SemanticSourceRouteAuditV111? route) => route is null ? null :
        route with
        {
            ActionIds = Clone(route.ActionIds, actionId => actionId)!,
            NativeEntryDecisions = Clone(route.NativeEntryDecisions,
                decision => decision with
                {
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])decision.Request
                            .OrderedCandidates.Clone()
                    }
                })!
        };

    private static T[]? Clone<T>(T[]? values, Func<T, T> clone)
        where T : class => values?.Select(value =>
            value is null ? null! : clone(value)).ToArray();
}
