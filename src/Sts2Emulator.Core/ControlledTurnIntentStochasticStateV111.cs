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
}
