using System.Text.Json.Serialization;

namespace Sts2Emulator.Core;

/// <summary>
/// Compatibility projection for the original index-only controlled-choice
/// payload. The typed stochastic continuation is the sole provider-state
/// owner; these members preserve established mechanics-facing accessors.
/// </summary>
public sealed record ControlledChanceContinuationStateV111
{
    private ControlledStochasticContinuationStateV111 _stochastic;
    private int? _decisionCandidateLabelCount;

    public ControlledChanceContinuationStateV111(
        RandomIndexDecisionV111[] decisions,
        RandomIndexRequestV111 pendingRequest,
        string[]? pendingCandidateLabels = null,
        string[][]? decisionCandidateLabels = null)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(pendingRequest);
        _decisionCandidateLabelCount = decisionCandidateLabels?.Length;
        _stochastic = CreateStochastic(
            decisions, pendingRequest, pendingCandidateLabels,
            decisionCandidateLabels);
    }

    [JsonIgnore]
    public ControlledStochasticContinuationStateV111 Stochastic
    {
        get => _stochastic;
        init => _stochastic = value ?? throw new ArgumentNullException(
            nameof(value));
    }

    public RandomIndexDecisionV111[] Decisions
    {
        get => _stochastic.Decisions.Select(decision =>
            decision.IndexDecision ?? throw new InvalidDataException(
                "Index-only chance projection contains a non-index outcome.")).ToArray();
        init => _stochastic = CreateStochastic(
            value, PendingRequest, PendingCandidateLabels,
            DecisionCandidateLabels);
    }

    public RandomIndexRequestV111 PendingRequest
    {
        get => _stochastic.PendingRequest.IndexRequest ??
            throw new InvalidDataException(
                "Index-only chance projection contains a non-index request.");
        init => _stochastic = CreateStochastic(
            Decisions, value, PendingCandidateLabels,
            DecisionCandidateLabels);
    }

    public string[]? PendingCandidateLabels
    {
        get => _stochastic.PendingCandidateLabels;
        init => _stochastic = _stochastic with
        {
            PendingCandidateLabels = value is null ? null : (string[])value.Clone()
        };
    }

    public string[][]? DecisionCandidateLabels
    {
        get
        {
            if (_decisionCandidateLabelCount is not { } count)
                return null;
            var labels = new string[count][];
            for (var index = 0; index < count; index++)
            {
                labels[index] = index < _stochastic.Decisions.Length &&
                    _stochastic.Decisions[index].CandidateLabels is { } saved
                    ? (string[])saved.Clone()
                    : null!;
            }
            return labels;
        }
        init
        {
            _decisionCandidateLabelCount = value?.Length;
            _stochastic = CreateStochastic(
                Decisions, PendingRequest, PendingCandidateLabels, value);
        }
    }

    public ControlledChanceContinuationStateV111 Fork() => this with
    {
        Stochastic = _stochastic.Fork()
    };

    public void Deconstruct(
        out RandomIndexDecisionV111[] decisions,
        out RandomIndexRequestV111 pendingRequest,
        out string[]? pendingCandidateLabels,
        out string[][]? decisionCandidateLabels)
    {
        decisions = Decisions;
        pendingRequest = PendingRequest;
        pendingCandidateLabels = PendingCandidateLabels;
        decisionCandidateLabels = DecisionCandidateLabels;
    }

    private static ControlledStochasticContinuationStateV111 CreateStochastic(
        RandomIndexDecisionV111[] decisions,
        RandomIndexRequestV111 pendingRequest,
        string[]? pendingCandidateLabels,
        string[][]? decisionCandidateLabels)
    {
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(pendingRequest);
        var typedDecisions = decisions.Select((decision, index) =>
        {
            ArgumentNullException.ThrowIfNull(decision);
            string[]? labels = decisionCandidateLabels is { } transcript &&
                index < transcript.Length && transcript[index] is { } saved
                    ? (string[])saved.Clone()
                    : null;
            return ControlledStochasticDecisionV111.FromIndex(
                decision with
                {
                    Request = decision.Request with
                    {
                        OrderedCandidates = (int[])
                            decision.Request.OrderedCandidates.Clone()
                    }
                }, labels);
        }).ToArray();
        var typed = new ControlledStochasticContinuationStateV111(
            ControlledStochasticContinuationStateV111.CurrentSchemaId,
            typedDecisions,
            ControlledStochasticRequestV111.FromIndex(
                pendingRequest with
                {
                    OrderedCandidates = (int[])
                        pendingRequest.OrderedCandidates.Clone()
                }),
            pendingCandidateLabels is null ? null :
                (string[])pendingCandidateLabels.Clone());
        return typed;
    }
}
