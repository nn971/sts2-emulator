namespace Sts2Emulator.Core;

/// <summary>
/// Provider-owned request and decision prefix for a suspended next-turn
/// intent continuation. Its enclosing frame retains the mechanics checkpoint.
/// </summary>
public sealed record ControlledTurnIntentStochasticStateV111
{
    private RandomIndexDecisionV111[]? _transcript;

    [System.Text.Json.Serialization.JsonConstructor]
    public ControlledTurnIntentStochasticStateV111(
        RandomIndexDecisionV111[] Transcript,
        RandomIndexRequestV111? Pending)
    {
        _transcript = CloneDecisions(Transcript);
        this.Pending = Pending is null ? null : CloneRequest(Pending);
    }

    public RandomIndexDecisionV111[] Transcript
    {
        get => CloneDecisions(_transcript)!;
        init => _transcript = CloneDecisions(value);
    }

    public RandomIndexRequestV111? Pending { get; init; }

    public void Deconstruct(
        out RandomIndexDecisionV111[] Transcript,
        out RandomIndexRequestV111? Pending)
    {
        Transcript = this.Transcript;
        Pending = this.Pending;
    }

    public ControlledTurnIntentStochasticStateV111 Fork() => this with
    {
        Transcript = _transcript!,
        Pending = Pending is null ? null : CloneRequest(Pending)
    };

    public void Validate()
    {
        if (Transcript is null)
            throw new InvalidDataException(
                "Malformed controlled turn-intent continuation.");
        foreach (var decision in Transcript)
        {
            if (decision is null)
                throw new InvalidDataException(
                    "Turn-intent transcript contains a null decision.");
            decision.Validate();
            if (decision.ProviderMode != "controlled-v111" ||
                decision.NativeCursorBefore is not null ||
                decision.NativeCursorAfter is not null)
                throw new InvalidDataException(
                    "Turn-intent transcript contains a non-controlled outcome.");
        }
        Pending?.Validate();
    }

    internal ControlledTurnIntentStochasticStateV111 AppendResolvedDecision(
        RandomIndexDecisionV111 decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        decision.Validate();
        var transcript = _transcript ?? throw new InvalidDataException(
            "Turn-intent transcript is missing.");
        var pending = Pending ?? throw new InvalidOperationException(
            "No pending turn-intent request can be resolved.");
        if (decision.ProviderMode != "controlled-v111" ||
            decision.NativeCursorBefore is not null ||
            decision.NativeCursorAfter is not null ||
            decision.ProbabilityStatus != "UNVERIFIED" ||
            !ControlledTurnIntentEngineV111.IsSupportedSiteId(
                decision.Request.SiteId) ||
            decision.Request.StreamId != "run.monster_ai" ||
            decision.Request.Ordinal != transcript.Length ||
            !ControlledStochasticChanceReplayV111.RequestsMatch(
                decision.Request, pending))
            throw new InvalidDataException(
                "Turn-intent decision does not resolve the saved request prefix.");
        foreach (var previous in transcript)
        {
            if (previous is null)
                throw new InvalidDataException(
                    "Turn-intent transcript contains a null decision.");
            previous.Validate();
            if (previous.ProviderMode != "controlled-v111" ||
                previous.NativeCursorBefore is not null ||
                previous.NativeCursorAfter is not null)
                throw new InvalidDataException(
                    "Turn-intent transcript contains a non-controlled outcome.");
        }
        return this with
        {
            Transcript = transcript.Append(CloneDecision(decision)).ToArray(),
            Pending = null
        };
    }

    private static RandomIndexRequestV111 CloneRequest(
        RandomIndexRequestV111 request) => request with
    {
        OrderedCandidates = (int[])request.OrderedCandidates.Clone()
    };

    private static RandomIndexDecisionV111[]? CloneDecisions(
        RandomIndexDecisionV111[]? values) => values?.Select(decision =>
            decision is null ? null! :
                PrototypeControlledEventChoiceContinuationV111
                    .CloneDecision(decision)).ToArray();

    private static RandomIndexDecisionV111 CloneDecision(
        RandomIndexDecisionV111 decision) => decision with
    {
        Request = decision.Request with
        {
            OrderedCandidates = (int[])decision.Request.OrderedCandidates.Clone()
        }
    };
}
