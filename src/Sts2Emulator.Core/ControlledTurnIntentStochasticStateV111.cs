namespace Sts2Emulator.Core;

/// <summary>
/// Provider-owned request and decision prefix for a suspended next-turn
/// intent continuation. Its enclosing frame retains the mechanics checkpoint.
/// </summary>
public sealed record ControlledTurnIntentStochasticStateV111(
    RandomIndexDecisionV111[] Transcript,
    RandomIndexRequestV111? Pending)
{
    public ControlledTurnIntentStochasticStateV111 Fork() => this with
    {
        Transcript = Transcript.Select(decision => decision with
        {
            Request = CloneRequest(decision.Request)
        }).ToArray(),
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
}
